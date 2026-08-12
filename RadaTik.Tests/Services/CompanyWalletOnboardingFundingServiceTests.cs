using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class CompanyWalletOnboardingFundingServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WithSufficientBalance_IsSatisfied()
    {
        await using ApplicationDbContext db = CreateDb();
        await SeedPricingAsync(db, 100m);
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 150m });
        await db.SaveChangesAsync();

        CompanyWalletOnboardingFundingService sut = new(db);
        CompanyWalletOnboardingFundingStatus status = await sut.EvaluateAsync(1);

        Assert.True(status.IsSatisfied);
        Assert.False(status.RequiresFundingGate);
        Assert.Equal(100m, status.MinimumRequiredSyp);
        Assert.Equal(150m, status.CurrentBalanceSyp);
        Assert.False(status.HasSufficientPendingRequest);
    }

    [Fact]
    public async Task EvaluateAsync_WithSufficientPendingRequest_IsSatisfied()
    {
        await using ApplicationDbContext db = CreateDb();
        await SeedPricingAsync(db, 100m);
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 0m });
        db.NetworkTopUpRequests.Add(new NetworkTopUpRequest
        {
            NetworkId = 1,
            Amount = 100m,
            Status = NetworkTopUpRequestStatus.Pending,
            RequestedByUserId = "user-1",
            RequestedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        CompanyWalletOnboardingFundingService sut = new(db);
        CompanyWalletOnboardingFundingStatus status = await sut.EvaluateAsync(1);

        Assert.True(status.IsSatisfied);
        Assert.False(status.RequiresFundingGate);
        Assert.True(status.HasSufficientPendingRequest);
        Assert.Equal(0m, status.CurrentBalanceSyp);
    }

    [Fact]
    public async Task EvaluateAsync_WithInsufficientPendingRequest_IsNotSatisfied()
    {
        await using ApplicationDbContext db = CreateDb();
        await SeedPricingAsync(db, 100m);
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 0m });
        db.NetworkTopUpRequests.Add(new NetworkTopUpRequest
        {
            NetworkId = 1,
            Amount = 50m,
            Status = NetworkTopUpRequestStatus.Pending,
            RequestedByUserId = "user-1",
            RequestedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        CompanyWalletOnboardingFundingService sut = new(db);
        CompanyWalletOnboardingFundingStatus status = await sut.EvaluateAsync(1);

        Assert.False(status.IsSatisfied);
        Assert.True(status.RequiresFundingGate);
        Assert.False(status.HasSufficientPendingRequest);
        Assert.Equal(100m, status.MinimumRequiredSyp);
    }

    [Fact]
    public async Task EvaluateAsync_WithZeroCatalogPrice_IsSatisfiedWithoutGate()
    {
        await using ApplicationDbContext db = CreateDb();
        await SeedPricingAsync(db, 0m);
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 0m });
        await db.SaveChangesAsync();

        CompanyWalletOnboardingFundingService sut = new(db);
        CompanyWalletOnboardingFundingStatus status = await sut.EvaluateAsync(1);

        Assert.True(status.IsSatisfied);
        Assert.False(status.RequiresFundingGate);
        Assert.Equal(0m, status.MinimumRequiredSyp);
    }

    [Fact]
    public async Task GetRequiredMinimumSypAsync_ReturnsCeiledCatalogAmount()
    {
        await using ApplicationDbContext db = CreateDb();
        await SeedPricingAsync(db, 99.1m);

        CompanyWalletOnboardingFundingService sut = new(db);
        decimal min = await sut.GetRequiredMinimumSypAsync();

        Assert.Equal(100m, min);
    }

    private static async Task SeedPricingAsync(ApplicationDbContext db, decimal amountSyp)
    {
        db.FeaturePricings.Add(new FeaturePricing
        {
            FeatureKey = FeatureKeys.Networks,
            BillingPeriod = PricingBillingPeriod.OneTime,
            ChargeUnit = PricingChargeUnit.PerNetwork,
            AmountSYP = amountSyp,
            IsActive = true
        });
        await db.SaveChangesAsync();
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
