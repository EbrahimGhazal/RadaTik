using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RadaTik.Data;
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
    public async Task DeleteClientAsync_RemovesClientAndCallsMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(7, "del-me", 2, 9));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik.Setup(m => m.DeletePPPoEUser("del-me", 9)).ReturnsAsync(true);

        ClientProvisioningService sut = CreateSut(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.DeleteClientAsync(7, 2);

        Assert.True(outcome.IsSuccess);
        Assert.Empty(await db.Clients.ToListAsync());
        mikroTik.Verify(m => m.DeletePPPoEUser("del-me", 9), Times.Once);
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

    private static Client MinimalClient(int id, string userName, int networkId, int? serverId = null) =>
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
            MikroTikServerId = serverId
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
