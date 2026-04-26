using RadTik.Services.PricingPolicies;
using Xunit;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Tests.Services;

public class SenderPricingOrchestratorTests
{
    [Fact]
    public void ParsePendingSectorMeta_ReturnsSectorId_WhenMarkerExists()
    {
        const string notes = "SECTOR_CREATE_PENDING:123;Network:7";

        var ok = SenderPricingOrchestrator.TryParsePendingSectorMeta(notes, out var sectorId);

        Assert.True(ok);
        Assert.Equal(123, sectorId);
    }

    [Fact]
    public void ParsePendingSectorMeta_ReturnsFalse_WhenMarkerMissing()
    {
        var ok = SenderPricingOrchestrator.TryParsePendingSectorMeta("regular note", out var sectorId);

        Assert.False(ok);
        Assert.Equal(0, sectorId);
    }

    [Fact]
    public void SenderCreationStrategies_RespectActorType()
    {
        var immediate = new ImmediateSenderCreationWorkflowStrategy();
        var approval = new ApprovalGatedSenderCreationWorkflowStrategy();

        Assert.True(immediate.CanHandle(actorIsEmployee: false));
        Assert.False(immediate.CanHandle(actorIsEmployee: true));
        Assert.True(approval.CanHandle(actorIsEmployee: true));
        Assert.False(approval.CanHandle(actorIsEmployee: false));
    }

    [Fact]
    public async Task TryHandlePendingApprovalAsync_WithSufficientBalance_ActivatesSectorAndChargesWallet()
    {
        await using var db = CreateDb();
        SeedApprovalScenario(db, companyBalance: 1000m, priceSyp: 250m);

        var orchestrator = CreateOrchestrator(db);
        var request = await db.NetworkServiceRequests.FirstAsync();

        var result = await orchestrator.TryHandlePendingApprovalAsync(request, "sys-admin", null);

        Assert.Equal(SenderApprovalOutcomeType.ApprovedAndCharged, result.OutcomeType);

        var sector = await db.Sectors.FirstAsync();
        var company = await db.Networks.FirstAsync(n => n.Id == 1);
        var walletTx = await db.NetworkWalletTransactions.FirstOrDefaultAsync();

        Assert.True(sector.IsActive);
        Assert.Equal(750m, company.Balance);
        Assert.NotNull(walletTx);
        Assert.Equal(-250m, walletTx!.SignedAmount);
        Assert.Equal(NetworkServiceRequestStatus.Approved, request.Status);
        Assert.NotNull(request.ChargeWalletTransactionId);
    }

    [Fact]
    public async Task TryHandlePendingApprovalAsync_WithInsufficientBalance_ReturnsErrorWithoutActivation()
    {
        await using var db = CreateDb();
        SeedApprovalScenario(db, companyBalance: 100m, priceSyp: 250m);

        var orchestrator = CreateOrchestrator(db);
        var request = await db.NetworkServiceRequests.FirstAsync();

        var result = await orchestrator.TryHandlePendingApprovalAsync(request, "sys-admin", null);

        Assert.Equal(SenderApprovalOutcomeType.InsufficientBalance, result.OutcomeType);

        var sector = await db.Sectors.FirstAsync();
        var company = await db.Networks.FirstAsync(n => n.Id == 1);
        var walletTx = await db.NetworkWalletTransactions.FirstOrDefaultAsync();

        Assert.False(sector.IsActive);
        Assert.Equal(100m, company.Balance);
        Assert.Null(walletTx);
        Assert.Equal(NetworkServiceRequestStatus.Pending, request.Status);
        Assert.Null(request.ChargeWalletTransactionId);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static SenderPricingOrchestrator CreateOrchestrator(ApplicationDbContext db)
    {
        var strategies = new ISenderCreationWorkflowStrategy[]
        {
            new ImmediateSenderCreationWorkflowStrategy(),
            new ApprovalGatedSenderCreationWorkflowStrategy()
        };

        return new SenderPricingOrchestrator(db, strategies);
    }

    private static void SeedApprovalScenario(ApplicationDbContext db, decimal companyBalance, decimal priceSyp)
    {
        db.Networks.AddRange(
            new Network { Id = 1, Name = "Company", Balance = companyBalance },
            new Network { Id = 2, Name = "Branch", ParentNetworkId = 1, Balance = 0m });

        db.Sectors.Add(new Sector
        {
            Id = 10,
            Name = "S1",
            NetworkId = 2,
            IsActive = false,
            MikroTikServerId = 1,
            IPAddress = "10.1.1.10",
            NetworkMask = "255.255.255.0",
            Latitude = 0,
            Longitude = 0,
            Direction = 0,
            CoverageAngle = 120,
            CoverageRange = 1
        });

        db.FeaturePricings.Add(new FeaturePricing
        {
            Id = 90,
            FeatureKey = FeatureKeys.Sectors,
            BillingPeriod = PricingBillingPeriod.OneTime,
            ChargeUnit = PricingChargeUnit.PerSector,
            AmountSYP = priceSyp,
            AmountUSD = 0m,
            Currency = PricingCurrency.SYP_New,
            IsActive = true
        });

        db.NetworkServiceRequests.Add(new NetworkServiceRequest
        {
            Id = 100,
            NetworkId = 1,
            FeatureKey = FeatureKeys.Sectors,
            FeaturePricingId = 90,
            BillingPeriod = PricingBillingPeriod.OneTime,
            AmountSYP = priceSyp,
            AmountUSD = 0m,
            Currency = PricingCurrency.SYP_New,
            Status = NetworkServiceRequestStatus.Pending,
            RequestedByUserId = "emp-1",
            RequestedAt = DateTime.Now,
            Notes = "SECTOR_CREATE_PENDING:10;Network:2"
        });

        db.SaveChanges();
    }
}
