using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Approvals;
using RadaTik.Services.MikroTik;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientProvisioningServiceTests
{
    [Fact]
    public async Task DeleteClientAsync_WithInstallationInvoice_RemovesInvoiceThenClient()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(7, "del-me", 2, 9));
        db.SubscriberInstallationInvoices.Add(new SubscriberInstallationInvoice
        {
            Id = 100,
            ClientId = 7,
            NetworkId = 2,
            CompanyName = "Co",
            ClientName = "del-me",
            ReceiverMode = SubscriberReceiverMode.Private,
            Kind = SubscriberInstallationInvoiceKind.InitialSetup,
            Status = SubscriberInstallationInvoiceStatus.Draft,
            CreatedByUserId = "u1",
            TotalAmount = 10,
            RemainingAmount = 10
        });
        db.SubscriberInstallationInvoiceItems.Add(new SubscriberInstallationInvoiceItem
        {
            Id = 1,
            SubscriberInstallationInvoiceId = 100,
            ItemName = "كابل",
            Quantity = 1,
            UnitPrice = 10,
            LineTotal = 10
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik.Setup(m => m.DeletePPPoEUser("del-me", 9)).ReturnsAsync(true);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.DeleteClientAsync(7, 2);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(await db.Clients.ToListAsync());
        Assert.Empty(await db.SubscriberInstallationInvoices.ToListAsync());
        Assert.Empty(await db.SubscriberInstallationInvoiceItems.ToListAsync());
    }

    [Fact]
    public async Task DeleteClientAsync_RemovesClientAndCallsMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(17, "plain-del", 2, 9));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik.Setup(m => m.DeletePPPoEUser("plain-del", 9)).ReturnsAsync(true);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.DeleteClientAsync(17, 2);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(await db.Clients.ToListAsync());
        mikroTik.Verify(m => m.DeletePPPoEUser("plain-del", 9), Times.Once);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenMikroTikUnreachable_StillDeletesFromDatabase()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(8, "gone", 2, 9));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.DeletePPPoEUser("gone", 9))
            .ThrowsAsync(new InvalidOperationException("فشل الاتصال بالخادم 10.0.0.1 بعد 3 محاولات"));

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.DeleteClientAsync(8, 2);

        Assert.True(outcome.IsSuccess);
        Assert.Contains("MikroTik", outcome.SuccessMessage);
        Assert.Empty(await db.Clients.ToListAsync());
    }

    [Fact]
    public async Task DeleteClientAsync_WhenLastDuplicateRemoved_ClearsFlagOnRemainingClient()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(1, "same-user", 2, 9, isCrossServerDuplicate: true));
        db.Clients.Add(MinimalClient(2, "same-user", 2, 10, isCrossServerDuplicate: true));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik.Setup(m => m.DeletePPPoEUser("same-user", 10)).ReturnsAsync(true);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.DeleteClientAsync(2, 2);

        Assert.True(outcome.IsSuccess);
        Client remaining = Assert.Single(await db.Clients.ToListAsync());
        Assert.Equal(1, remaining.Id);
        Assert.False(remaining.IsCrossServerDuplicate);
    }

    [Fact]
    public async Task DeleteClientAsync_WhenOtherDuplicatesRemain_KeepsFlag()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(1, "same-user", 2, 9, isCrossServerDuplicate: true));
        db.Clients.Add(MinimalClient(2, "same-user", 2, 10, isCrossServerDuplicate: true));
        db.Clients.Add(MinimalClient(3, "same-user", 2, 11, isCrossServerDuplicate: true));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik.Setup(m => m.DeletePPPoEUser("same-user", 11)).ReturnsAsync(true);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.DeleteClientAsync(3, 2);

        Assert.True(outcome.IsSuccess);
        List<Client> remaining = await db.Clients.OrderBy(c => c.Id).ToListAsync();
        Assert.Equal(2, remaining.Count);
        Assert.All(remaining, c => Assert.True(c.IsCrossServerDuplicate));
    }

    [Fact]
    public async Task DeleteClientAsync_UnknownClient_ReturnsNotFound()
    {
        await using ApplicationDbContext db = CreateDb();
        ClientProvisioningService sut = CreateSut(db, Mock.Of<IMikroTikPppoeUserService>());

        ClientOperationOutcome outcome = await sut.DeleteClientAsync(99, 2);

        Assert.True(outcome.NotFound);
    }

    [Fact]
    public async Task UpdateClientAsync_Employee_SubmitsApprovalRequest()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.Clients.Add(MinimalClient(5, "u5", 2, 1));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        Mock<IEmployeeServiceApprovalRequestService> approvals = new(MockBehavior.Strict);
        approvals
            .Setup(a => a.CreatePendingAsync(2, "emp-1", FeatureKeys.Clients, It.IsAny<string>(), 0m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object, approvals.Object);
        Client submitted = new()
        {
            Id = 5,
            Name = "New Name",
            UserName = "u5-new",
            ProfileId = 1
        };

        ClientEditOutcome outcome = await sut.UpdateClientAsync(new ClientEditRequest
        {
            ClientId = 5,
            SubmittedClient = submitted,
            NetworkId = 2,
            ActorUserId = "emp-1",
            IsEmployee = true
        });

        Assert.Equal(ClientEditStatus.EmployeePendingApproval, outcome.Status);
        approvals.VerifyAll();
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateClientAsync_AdminWithoutMikroTikFlag_SavesDatabaseOnly()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.Profiles.Add(new Profile { Id = 1, Name = "10M", NetworkId = 2 });
        db.Clients.Add(MinimalClient(5, "keep-user", 2, 9));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        Client submitted = new()
        {
            Id = 5,
            Name = "اسم جديد",
            UserName = "hacked-user",
            Password = "new-pass",
            ProfileId = 1,
            PhoneNumber = "0999999999",
            IsActive = false
        };

        ClientEditOutcome outcome = await sut.UpdateClientAsync(new ClientEditRequest
        {
            ClientId = 5,
            SubmittedClient = submitted,
            NetworkId = 2,
            ActorUserId = "admin-1",
            IsEmployee = false,
            ApplyMikroTikChanges = false
        });

        Assert.Equal(ClientEditStatus.Success, outcome.Status);
        Assert.Contains("دون تغيير إعدادات MikroTik", outcome.Message);
        Client saved = await db.Clients.SingleAsync(c => c.Id == 5);
        Assert.Equal("اسم جديد", saved.Name);
        Assert.Equal("keep-user", saved.UserName);
        Assert.Equal("pass", saved.Password);
        Assert.True(saved.IsActive);
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateClientAsync_AdminWithMikroTikFlag_PushesToMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.Profiles.Add(new Profile { Id = 1, Name = "10M", NetworkId = 2 });
        db.Clients.Add(MinimalClient(5, "keep-user", 2, 9));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik.Setup(m => m.UpdatePPPoEUser(It.IsAny<Client>())).ReturnsAsync(true);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        Client submitted = new()
        {
            Id = 5,
            Name = "اسم جديد",
            UserName = "keep-user",
            Password = "new-pass",
            ProfileId = 1,
            PhoneNumber = "0999999999",
            MikroTikServerId = 9,
            IsActive = true
        };

        ClientEditOutcome outcome = await sut.UpdateClientAsync(new ClientEditRequest
        {
            ClientId = 5,
            SubmittedClient = submitted,
            NetworkId = 2,
            ActorUserId = "admin-1",
            IsEmployee = false,
            ApplyMikroTikChanges = true
        });

        Assert.Equal(ClientEditStatus.Success, outcome.Status);
        Assert.Contains("المايكروتك", outcome.Message);
        Client saved = await db.Clients.SingleAsync(c => c.Id == 5);
        Assert.Equal("اسم جديد", saved.Name);
        Assert.Equal("new-pass", saved.Password);
        mikroTik.Verify(m => m.UpdatePPPoEUser(It.Is<Client>(c => c.Id == 5 && c.Password == "new-pass")), Times.Once);
    }

    [Fact]
    public async Task UpdateClientAsync_Employee_CannotChangeMikroTikUsernameOrPassword()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.Clients.Add(MinimalClient(5, "u5", 2, 1));
        await db.SaveChangesAsync();

        string? capturedNotes = null;
        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        Mock<IEmployeeServiceApprovalRequestService> approvals = new(MockBehavior.Strict);
        approvals
            .Setup(a => a.CreatePendingAsync(2, "emp-1", FeatureKeys.Clients, It.IsAny<string>(), 0m, It.IsAny<CancellationToken>()))
            .Callback<int, string, string, string, decimal, CancellationToken>((_, _, _, notes, _, _) => capturedNotes = notes)
            .ReturnsAsync(42);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object, approvals.Object);
        ClientEditOutcome outcome = await sut.UpdateClientAsync(new ClientEditRequest
        {
            ClientId = 5,
            SubmittedClient = new Client
            {
                Id = 5,
                Name = "اسم شخصي",
                UserName = "hacked-mt-user",
                Password = "hacked-mt-pass",
                ProfileId = 1,
                PhoneNumber = "0911111111"
            },
            NetworkId = 2,
            ActorUserId = "emp-1",
            IsEmployee = true
        });

        Assert.Equal(ClientEditStatus.EmployeePendingApproval, outcome.Status);
        Assert.True(EmployeeApprovalRequestHelper.TryParse(capturedNotes, out _, out _, out string? payloadJson));
        ClientApprovalPayload? payload = EmployeeApprovalRequestHelper.DeserializePayload<ClientApprovalPayload>(payloadJson);
        Assert.NotNull(payload);
        Assert.Equal("u5", payload!.UserName);
        Assert.Null(payload.Password);
        Assert.Equal("اسم شخصي", payload.Name);
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdateClientAsync_EmployeeFlagCannotForceMikroTikPush()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.Clients.Add(MinimalClient(5, "u5", 2, 1));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        Mock<IEmployeeServiceApprovalRequestService> approvals = new(MockBehavior.Strict);
        approvals
            .Setup(a => a.CreatePendingAsync(2, "emp-1", FeatureKeys.Clients, It.IsAny<string>(), 0m, It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object, approvals.Object);
        ClientEditOutcome outcome = await sut.UpdateClientAsync(new ClientEditRequest
        {
            ClientId = 5,
            SubmittedClient = new Client { Id = 5, Name = "New Name", UserName = "u5-new", ProfileId = 1 },
            NetworkId = 2,
            ActorUserId = "emp-1",
            IsEmployee = true,
            ApplyMikroTikChanges = true
        });

        Assert.Equal(ClientEditStatus.EmployeePendingApproval, outcome.Status);
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateClientAsync_Employee_CreatesPendingRequestWithoutMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.Profiles.Add(new Profile
        {
            Id = 1,
            Name = "10M",
            NetworkId = 2,
            MikroTikServerId = 9,
            IsActive = true,
            Type = ProfileType.Internet,
            BillingCycle = BillingCycle.Monthly,
            Price = 0,
            DownloadSpeed = 10
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        Mock<IEmployeeServiceApprovalRequestService> approvals = new(MockBehavior.Strict);
        string? capturedNotes = null;
        approvals
            .Setup(a => a.CreatePendingAsync(2, "emp-1", FeatureKeys.Clients, It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .Callback<int, string, string, string, decimal, CancellationToken>((_, _, _, notes, _, _) => capturedNotes = notes)
            .ReturnsAsync(42);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object, approvals.Object);
        ClientCreateOutcome outcome = await sut.CreateClientAsync(new ClientCreateRequest
        {
            Client = new Client
            {
                Name = "مشترك موظف",
                SID = "1234567890",
                UserName = "emp-create",
                Password = "secret1",
                ProfileId = 1,
                PhoneNumber = "0999999999",
                MikroTikServerId = 9
            },
            NetworkId = 2,
            ActorUserId = "emp-1",
            IsEmployee = true
        });

        Assert.Equal(ClientCreateStatus.EmployeePendingApproval, outcome.Status);
        Client saved = await db.Clients.SingleAsync(c => c.UserName == "emp-create");
        Assert.False(saved.IsActive);
        Assert.Equal(EmployeeApprovalStates.PendingClientConnectionStatus, saved.ConnectionStatus);
        Assert.True(EmployeeApprovalRequestHelper.TryParse(capturedNotes, out EmployeeApprovalRequestKind kind, out int entityId, out string? payloadJson));
        Assert.Equal(EmployeeApprovalRequestKind.ClientCreate, kind);
        Assert.Equal(saved.Id, entityId);
        Assert.True(string.IsNullOrWhiteSpace(payloadJson));
        mikroTik.VerifyNoOtherCalls();
        approvals.VerifyAll();
    }

    [Fact]
    public async Task ValidateForCreate_MissingRequiredFields_ReturnsErrors()
    {
        await using ApplicationDbContext db = CreateDb();
        ClientProvisioningService sut = CreateSut(db, Mock.Of<IMikroTikPppoeUserService>());
        ClientValidationResult result = sut.ValidateForCreate(new Client());
        Assert.False(result.IsValid);
        Assert.True(result.Errors.ContainsKey("Name"));
        Assert.True(result.Errors.ContainsKey("UserName"));
    }

    private static ClientProvisioningService CreateSut(
        ApplicationDbContext db,
        IMikroTikPppoeUserService mikroTik,
        IEmployeeServiceApprovalRequestService? approvals = null) =>
        new(
            db,
            mikroTik,
            BuildUserManagerMock().Object,
            Mock.Of<IUsageBasedSubscriptionChargeService>(),
            approvals ?? Mock.Of<IEmployeeServiceApprovalRequestService>(),
            Mock.Of<ILogger<ClientProvisioningService>>());

    private static Client MinimalClient(
        int id,
        string userName,
        int networkId,
        int? serverId = null,
        bool isCrossServerDuplicate = false) =>
        new()
        {
            Id = id,
            Name = userName,
            UserName = userName,
            Password = "pass",
            SID = "1234567890",
            PhoneNumber = "0999999999",
            NetworkId = networkId,
            ProfileId = 1,
            MikroTikServerId = serverId,
            IsCrossServerDuplicate = isCrossServerDuplicate
        };

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
    {
        Mock<IUserStore<ApplicationUser>> store = new();
        return new Mock<UserManager<ApplicationUser>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
