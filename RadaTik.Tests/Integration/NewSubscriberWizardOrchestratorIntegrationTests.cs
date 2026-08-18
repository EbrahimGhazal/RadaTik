using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.MikroTik;
using RadaTik.Services.NewSubscriberWizard;
using Xunit;

namespace RadaTik.Tests.Integration;

public class NewSubscriberWizardOrchestratorIntegrationTests
{
    [Fact]
    public async Task CreateSubscriberAsync_WhenDuplicateInSameCompany_ReturnsServerNameInError()
    {
        await using ApplicationDbContext db = CreateDbContext();
        SeedCompanyScope(db);

        db.MikroTikServers.Add(new MikroTikServer { Id = 100, Name = "Server-A", Host = "1.1.1.1", User = "u", Pass = "p", NetworkId = 2 });
        db.MikroTikServers.Add(new MikroTikServer { Id = 200, Name = "Server-B", Host = "4.4.4.4", User = "u", Pass = "p", NetworkId = 2 });
        db.Profiles.Add(new Profile
        {
            Id = 11,
            Name = "P-A",
            NetworkId = 2,
            MikroTikServerId = 100,
            IsActive = true,
            Type = ProfileType.Internet,
            BillingCycle = BillingCycle.Monthly,
            Price = 0,
            DownloadSpeed = 10
        });
        db.Profiles.Add(new Profile
        {
            Id = 12,
            Name = "P-B",
            NetworkId = 2,
            MikroTikServerId = 200,
            IsActive = true,
            Type = ProfileType.Internet,
            BillingCycle = BillingCycle.Monthly,
            Price = 0,
            DownloadSpeed = 10
        });
        db.Clients.Add(new Client
        {
            Name = "Existing",
            SID = "111",
            UserName = "dup-user",
            Password = "secret",
            ProfileId = 11,
            ProfileName = "P-A",
            PhoneNumber = "099",
            NetworkId = 2,
            MikroTikServerId = 100
        });
        await db.SaveChangesAsync();

        NewSubscriberWizardOrchestrator orchestrator = BuildOrchestrator(
            db,
            BuildUserManagerMock(),
            new Mock<IMikroTikPppoeUserService>(MockBehavior.Strict),
            BuildInvoiceMock(),
            BuildUsageChargeMock());

        Client newClient = BuildClient("dup-user", serverId: 200);
        newClient.ProfileId = 12;
        ApplicationUser actor = new() { Id = "actor-1", UserName = "admin" };

        NewSubscriberWizardOrchestrator.CreateSubscriberResult result = await orchestrator.CreateSubscriberAsync(
            newClient,
            actor,
            networkId: 2,
            path: NewSubscriberWizardPath.TowerDirect,
            dbUserName: null,
            dbPassword: null);

        Assert.False(result.Success);
        Assert.Contains("Server-A", result.ErrorMessage);
    }

    [Fact]
    public async Task CreateSubscriberAsync_WhenValid_PersistsClientAndReturnsSuccess()
    {
        await using ApplicationDbContext db = CreateDbContext();
        SeedCompanyScope(db);
        await db.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = BuildUserManagerMock();
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([RoleNames.NetworkAdministrator]);
        userManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Client"))
            .ReturnsAsync(IdentityResult.Success);

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        Mock<ISubscriberInstallationInvoiceService> invoice = BuildInvoiceMock();
        Mock<IUsageBasedSubscriptionChargeService> usage = BuildUsageChargeMock();

        NewSubscriberWizardOrchestrator orchestrator = BuildOrchestrator(db, userManager, mikroTik, invoice, usage);

        Client newClient = BuildClient("new-user", serverId: null);
        ApplicationUser actor = new() { Id = "actor-1", UserName = "admin" };

        NewSubscriberWizardOrchestrator.CreateSubscriberResult result = await orchestrator.CreateSubscriberAsync(
            newClient,
            actor,
            networkId: 2,
            path: NewSubscriberWizardPath.TowerDirect,
            dbUserName: null,
            dbPassword: null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.ClientId);
        Assert.Equal(1, await db.Clients.CountAsync(c => c.UserName == "new-user"));
        invoice.Verify(i => i.CreateDraftInitialSetupInvoiceAsync(It.IsAny<Client>(), NewSubscriberWizardPath.TowerDirect, actor.Id, It.IsAny<CancellationToken>()), Times.Once);
        usage.Verify(u => u.ChargeUsageIncreaseAsync(1, actor.Id, PricingChargeUnit.PerSubscriber, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateSubscriberAsync_WhenMikroTikUnreachable_StillPersistsClient()
    {
        await using ApplicationDbContext db = CreateDbContext();
        SeedCompanyScope(db);
        await db.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = BuildUserManagerMock();
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([RoleNames.NetworkAdministrator]);
        userManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Client"))
            .ReturnsAsync(IdentityResult.Success);

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.AddPPPoEUser(It.Is<Client>(c => c.UserName == "tower-user")))
            .ThrowsAsync(new InvalidOperationException("فشل الاتصال بالخادم 10.0.0.1 بعد 3 محاولات"));

        NewSubscriberWizardOrchestrator orchestrator = BuildOrchestrator(
            db,
            userManager,
            mikroTik,
            BuildInvoiceMock(),
            BuildUsageChargeMock());

        NewSubscriberWizardOrchestrator.CreateSubscriberResult result = await orchestrator.CreateSubscriberAsync(
            BuildClient("tower-user", serverId: 50),
            new ApplicationUser { Id = "actor-1", UserName = "admin" },
            networkId: 2,
            path: NewSubscriberWizardPath.TowerDirect,
            dbUserName: null,
            dbPassword: null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(result.MikroTikSynced);
        Assert.Contains("MikroTik", result.MikroTikWarning);
        Assert.Equal(1, await db.Clients.CountAsync(c => c.UserName == "tower-user"));
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task CreateSubscriberAsync_WhenMikroTikUserAlreadyExists_TreatsAsSynced()
    {
        await using ApplicationDbContext db = CreateDbContext();
        SeedCompanyScope(db);
        await db.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = BuildUserManagerMock();
        userManager.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([RoleNames.NetworkAdministrator]);
        userManager.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        userManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Client"))
            .ReturnsAsync(IdentityResult.Success);

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.AddPPPoEUser(It.Is<Client>(c => c.UserName == "existing-mt")))
            .ThrowsAsync(new InvalidOperationException("المستخدم existing-mt موجود مسبقاً في الخادم"));

        NewSubscriberWizardOrchestrator orchestrator = BuildOrchestrator(
            db,
            userManager,
            mikroTik,
            BuildInvoiceMock(),
            BuildUsageChargeMock());

        NewSubscriberWizardOrchestrator.CreateSubscriberResult result = await orchestrator.CreateSubscriberAsync(
            BuildClient("existing-mt", serverId: 50),
            new ApplicationUser { Id = "actor-1", UserName = "admin" },
            networkId: 2,
            path: NewSubscriberWizardPath.TowerDirect,
            dbUserName: null,
            dbPassword: null);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(result.MikroTikSynced);
        Assert.Null(result.MikroTikWarning);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task CreateSubscriberAsync_WhenProfileBelongsToAnotherServer_ReturnsError()
    {
        await using ApplicationDbContext db = CreateDbContext();
        SeedCompanyScope(db);
        db.MikroTikServers.Add(new MikroTikServer { Id = 60, Name = "OtherSrv", Host = "3.3.3.3", User = "u", Pass = "p", NetworkId = 2 });
        await db.SaveChangesAsync();

        NewSubscriberWizardOrchestrator orchestrator = BuildOrchestrator(
            db,
            BuildUserManagerMock(),
            new Mock<IMikroTikPppoeUserService>(MockBehavior.Strict),
            BuildInvoiceMock(),
            BuildUsageChargeMock());

        NewSubscriberWizardOrchestrator.CreateSubscriberResult result = await orchestrator.CreateSubscriberAsync(
            BuildClient("wrong-profile", serverId: 60),
            new ApplicationUser { Id = "actor-1", UserName = "admin" },
            networkId: 2,
            path: NewSubscriberWizardPath.TowerDirect,
            dbUserName: null,
            dbPassword: null);

        Assert.False(result.Success);
        Assert.Contains("MikroTik", result.ErrorMessage);
        Assert.Equal(0, await db.Clients.CountAsync(c => c.UserName == "wrong-profile"));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"wizard-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedCompanyScope(ApplicationDbContext db)
    {
        db.Networks.AddRange(
            new Network { Id = 1, Name = "Company", Status = NetworkStatus.Active },
            new Network { Id = 2, Name = "Branch", ParentNetworkId = 1, Status = NetworkStatus.Active });
        db.MikroTikServers.Add(new MikroTikServer { Id = 50, Name = "BaseSrv", Host = "2.2.2.2", User = "u", Pass = "p", NetworkId = 2 });
        db.Profiles.Add(new Profile
        {
            Id = 10,
            Name = "P1",
            NetworkId = 2,
            MikroTikServerId = 50,
            IsActive = true,
            Type = ProfileType.Internet,
            BillingCycle = BillingCycle.Monthly,
            Price = 0,
            DownloadSpeed = 10
        });
    }

    private static Client BuildClient(string userName, int? serverId)
    {
        return new Client
        {
            Name = "Test Client",
            SID = "123456",
            UserName = userName,
            Password = "123456",
            ProfileId = 10,
            PhoneNumber = "0999999999",
            MikroTikServerId = serverId,
            IsActive = true
        };
    }

    private static NewSubscriberWizardOrchestrator BuildOrchestrator(
        ApplicationDbContext db,
        Mock<UserManager<ApplicationUser>> userManager,
        Mock<IMikroTikPppoeUserService> mikroTik,
        Mock<ISubscriberInstallationInvoiceService> invoice,
        Mock<IUsageBasedSubscriptionChargeService> usage)
    {
        return new NewSubscriberWizardOrchestrator(
            db,
            userManager.Object,
            mikroTik.Object,
            invoice.Object,
            usage.Object,
            NullLogger<NewSubscriberWizardOrchestrator>.Instance);
    }

    private static Mock<ISubscriberInstallationInvoiceService> BuildInvoiceMock()
    {
        Mock<ISubscriberInstallationInvoiceService> invoice = new();
        invoice.Setup(i => i.CreateDraftInitialSetupInvoiceAsync(It.IsAny<Client>(), It.IsAny<NewSubscriberWizardPath>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(99);
        return invoice;
    }

    private static Mock<IUsageBasedSubscriptionChargeService> BuildUsageChargeMock()
    {
        Mock<IUsageBasedSubscriptionChargeService> usage = new();
        usage.Setup(u => u.EstimateImportChargeAsync(It.IsAny<int>(), PricingChargeUnit.PerSubscriber, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageImportChargeEstimate
            {
                ImportableCount = 1,
                MatchedPricingsCount = 1,
                UnitPriceSyp = 0m,
                RequiredAmountSyp = 0m,
                WalletBalance = 100000m
            });
        usage.Setup(u => u.ChargeUsageIncreaseAsync(It.IsAny<int>(), It.IsAny<string>(), PricingChargeUnit.PerSubscriber, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return usage;
    }

    private static Mock<UserManager<ApplicationUser>> BuildUserManagerMock()
    {
        Mock<IUserStore<ApplicationUser>> store = new();
        Mock<UserManager<ApplicationUser>> mock = new(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mock.Setup(m => m.GetRolesAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync([RoleNames.NetworkAdministrator]);
        mock.Setup(m => m.FindByNameAsync(It.IsAny<string>()))
            .ReturnsAsync((ApplicationUser?)null);
        return mock;
    }
}
