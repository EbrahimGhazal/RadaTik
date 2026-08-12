using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class MainNetworkCreationBillingTests
{
    [Fact]
    public async Task TryApplyOneTimeCreationChargeAsync_WithZeroBalance_DoesNotCharge()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 0m });
        await db.SaveChangesAsync();

        bool charged = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
            db, 1, "شركة", "Networks", 10m, "actor-1");

        Assert.False(charged);
        Assert.Equal(0m, (await db.Networks.SingleAsync()).Balance);
        Assert.Empty(await db.NetworkWalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task TryApplyOneTimeCreationChargeAsync_WithSufficientBalance_ChargesOnce()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 15m });
        await db.SaveChangesAsync();

        bool first = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
            db, 1, "شركة", "Networks", 10m, "actor-1");
        bool second = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
            db, 1, "شركة", "Networks", 10m, "actor-1");

        Assert.True(first);
        Assert.False(second);
        Assert.Equal(5m, (await db.Networks.SingleAsync()).Balance);
        Assert.Single(await db.NetworkWalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task TryApplyOneTimeCreationChargeAsync_SeedsLedgerForMainNetworkUnit()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "شركة", Balance = 15m });
        db.NetworkServiceSubscriptions.Add(new NetworkServiceSubscription
        {
            Id = 10,
            NetworkId = 1,
            FeatureKey = "Networks",
            BillingPeriod = PricingBillingPeriod.Monthly,
            Status = NetworkServiceSubscriptionStatus.Active,
            StartAt = DateTime.Now.AddDays(-1),
            ExpiresAt = DateTime.Now.AddMonths(1),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();

        bool charged = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
            db, 1, "شركة", "Networks", 10m, "actor-1");

        Assert.True(charged);
        ServiceUnitChargeLedger ledger = Assert.Single(await db.ServiceUnitChargeLedgers.ToListAsync());
        Assert.Equal("N:1", ledger.UnitEntityKey);
        Assert.Equal(PricingChargeUnit.PerNetwork, ledger.ChargeUnit);
        Assert.NotNull(ledger.FirstChargedAt);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
