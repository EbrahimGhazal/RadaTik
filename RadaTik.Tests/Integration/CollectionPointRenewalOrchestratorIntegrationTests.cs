using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.CollectionPoint;
using Xunit;

namespace RadaTik.Tests.Integration;

public sealed class CollectionPointRenewalOrchestratorIntegrationTests
{
    [Fact]
    public async Task PayBillAsync_WhenInsufficientPointBalance_ReturnsError()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedRenewalScenario(db, pointBalance: 0m);

        CollectionPointRenewalOrchestrator orchestrator = BuildOrchestrator(db);

        CollectionPointOperationOutcome outcome = await orchestrator.PayBillAsync(
            new PayBillCommand(ClientId: 1, UserId: "cp-user"));

        Assert.False(outcome.IsSuccess);
        Assert.Contains("غير كافٍ", outcome.ErrorMessage ?? string.Empty);
    }

    private static CollectionPointRenewalOrchestrator BuildOrchestrator(ApplicationDbContext db)
    {
        Mock<ICollectionCommissionChargeService> commission = new();
        Mock<IClientRenewalGuardService> guard = new();
        guard.Setup(g => g.CheckBlockingInvoicesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenewalBlockResult { CanRenew = true });

        return new CollectionPointRenewalOrchestrator(
            db,
            new CollectionPaymentService(),
            commission.Object,
            guard.Object,
            new CurrencyHelperAdapter(),
            new CompanyFinancialService(db),
            new ClientVipPolicyService(db));
    }

    private static void SeedRenewalScenario(ApplicationDbContext db, decimal pointBalance)
    {
        db.Networks.Add(new Network
        {
            Id = 1,
            Name = "Company",
            DefaultUsdToSypExchangeRate = 15000m
        });
        db.Networks.Add(new Network
        {
            Id = 2,
            Name = "Branch",
            ParentNetworkId = 1
        });
        db.Profiles.Add(new Profile
        {
            Id = 10,
            Name = "10M",
            Price = 100m,
            VATPercentage = 0m,
            NetworkId = 2,
            IsActive = true
        });
        db.Clients.Add(new Client
        {
            Id = 1,
            Name = "Test Client",
            UserName = "user1",
            Password = "pass",
            SID = "1234567890",
            PhoneNumber = "0999999999",
            NetworkId = 2,
            ProfileId = 10,
            AccountCurrency = PricingCurrency.SYP_New,
            AccountExpirationDate = DateTime.Now.AddDays(-5)
        });
        db.CollectionPointAccounts.Add(new CollectionPointAccount
        {
            Id = 1,
            UserId = "cp-user",
            NetworkId = 2,
            Balance = pointBalance
        });
        db.SaveChanges();
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
