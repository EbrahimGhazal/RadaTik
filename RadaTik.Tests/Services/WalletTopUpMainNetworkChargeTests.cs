using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

/// <summary>
/// أول تغذية بعد إنشاء الشركة: الرصيد = مبلغ التعبئة − رسوم إنشاء الشبكة مرة واحدة فقط.
/// </summary>
public sealed class WalletTopUpMainNetworkChargeTests
{
    [Fact]
    public async Task FirstTopUp_DeductsCreationFeeOnce_NotAgainAsNewUnit()
    {
        await using ApplicationDbContext db = CreateDb();

        db.Networks.Add(new Network
        {
            Id = 1,
            Name = "شركة تجريبية",
            Balance = 10_000m,
            ParentNetworkId = null
        });
        db.FeaturePricings.Add(new FeaturePricing
        {
            Id = 1,
            FeatureKey = FeatureKeys.Networks,
            IsActive = true,
            BillingPeriod = PricingBillingPeriod.OneTime,
            ChargeUnit = PricingChargeUnit.PerNetwork,
            AmountSYP = 1_000m
        });
        db.FeaturePricings.Add(new FeaturePricing
        {
            Id = 2,
            FeatureKey = FeatureKeys.Networks,
            IsActive = true,
            BillingPeriod = PricingBillingPeriod.Monthly,
            ChargeUnit = PricingChargeUnit.PerNetwork,
            AmountSYP = 500m
        });
        db.NetworkServiceSubscriptions.Add(new NetworkServiceSubscription
        {
            Id = 1,
            NetworkId = 1,
            FeatureKey = FeatureKeys.Networks,
            BillingPeriod = PricingBillingPeriod.Monthly,
            Status = NetworkServiceSubscriptionStatus.Active,
            StartAt = DateTime.Now.AddDays(-1),
            ExpiresAt = DateTime.Now.AddMonths(1),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        bool charged = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
            db, 1, "شركة تجريبية", FeatureKeys.Networks, 1_000m, "actor-1");
        Assert.True(charged);
        Assert.Equal(9_000m, (await db.Networks.SingleAsync()).Balance);

        UsageBasedSubscriptionChargeService usage = new(db, NullLogger<UsageBasedSubscriptionChargeService>.Instance);
        await usage.ChargeUsageIncreaseAsync(1, "actor-1");

        Assert.Equal(9_000m, (await db.Networks.SingleAsync()).Balance);

        List<NetworkWalletTransaction> charges = await db.NetworkWalletTransactions
            .Where(t => t.Type == NetworkWalletTransactionType.ServiceCharge)
            .ToListAsync();
        Assert.Single(charges);
        Assert.Contains(MainNetworkCreationBilling.OneTimeChargeNoteMarker, charges[0].Notes);
        Assert.DoesNotContain(charges, t => t.Notes != null && t.Notes.Contains("خصم عنصر جديد"));
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
