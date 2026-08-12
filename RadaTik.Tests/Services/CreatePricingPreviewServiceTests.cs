using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPreview;
using Xunit;

namespace RadaTik.Tests.Services;

public class CreatePricingPreviewServiceTests
{
    [Fact]
    public async Task BuildAsync_WhenNetworksPricingConfigured_ReturnsInitialAndRenewalFlags()
    {
        await using ApplicationDbContext db = CreateDbContext();
        Network company = new() { Id = 1, Name = "شركة تجريبية", Balance = 0m };
        db.Networks.Add(company);
        db.FeaturePricings.AddRange(
            new FeaturePricing
            {
                FeatureKey = FeatureKeys.Networks,
                ChargeUnit = PricingChargeUnit.PerNetwork,
                BillingPeriod = PricingBillingPeriod.OneTime,
                AmountSYP = 5000m,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            },
            new FeaturePricing
            {
                FeatureKey = FeatureKeys.Networks,
                ChargeUnit = PricingChargeUnit.PerNetwork,
                BillingPeriod = PricingBillingPeriod.Monthly,
                AmountSYP = 1000m,
                IsActive = true,
                UpdatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        CreatePricingPreviewService service = new(db, [new FixedNetworksCounter()]);

        CreatePricingPreviewResult result = await service.BuildAsync(
            company.Id,
            FeatureKeys.Networks,
            PricingChargeUnit.PerNetwork,
            PricingPreviewCounterKeys.Networks);

        Assert.True(result.HasInitialPricing);
        Assert.True(result.HasRenewalPricing);
        Assert.Equal(5000m, result.InitialPriceSyp);
        Assert.Equal(1000m, result.RenewalPriceSyp);
    }

    [Fact]
    public async Task BuildAsync_WhenOnlyInitialPricing_ReturnsRenewalMissing()
    {
        await using ApplicationDbContext db = CreateDbContext();
        db.Networks.Add(new Network { Id = 2, Name = "شبكة", Balance = 0m });
        db.FeaturePricings.Add(new FeaturePricing
        {
            FeatureKey = FeatureKeys.Networks,
            ChargeUnit = PricingChargeUnit.PerNetwork,
            BillingPeriod = PricingBillingPeriod.OneTime,
            AmountSYP = 100m,
            IsActive = true,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        CreatePricingPreviewService service = new(db, [new FixedNetworksCounter()]);
        CreatePricingPreviewResult result = await service.BuildAsync(
            2,
            FeatureKeys.Networks,
            PricingChargeUnit.PerNetwork,
            PricingPreviewCounterKeys.Networks);

        Assert.True(result.HasInitialPricing);
        Assert.False(result.HasRenewalPricing);
    }

    private sealed class FixedNetworksCounter : IPricingPreviewUnitsCounterStrategy
    {
        public string Key => PricingPreviewCounterKeys.Networks;

        public Task<int> CountAsync(
            ApplicationDbContext db,
            IReadOnlyCollection<int> companyScopeNetworkIds,
            CancellationToken ct = default) =>
            Task.FromResult(companyScopeNetworkIds.Count);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
