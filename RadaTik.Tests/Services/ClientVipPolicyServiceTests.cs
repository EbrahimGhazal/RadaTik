using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientVipPolicyServiceTests
{
    [Fact]
    public void ApplyPackageDiscount_NonVip_ReturnsOriginal()
    {
        decimal result = ClientVipPricing.ApplyPackageDiscount(1000m, isVip: false, new CompanyVipPolicy(20m, 7, false));
        Assert.Equal(1000m, result);
    }

    [Fact]
    public void ApplyPackageDiscount_Vip_AppliesPercentThenRounds()
    {
        decimal result = ClientVipPricing.ApplyPackageDiscount(1000m, isVip: true, new CompanyVipPolicy(10m, 0, false));
        Assert.Equal(900m, result);
    }

    [Fact]
    public void ApplyMonthlyPrice_ComputesVatAfterDiscount()
    {
        (decimal basePrice, decimal vat, decimal total) = ClientVipPricing.ApplyMonthlyPrice(
            1000m,
            10m,
            isVip: true,
            new CompanyVipPolicy(10m, 0, false));

        Assert.Equal(900m, basePrice);
        Assert.Equal(90m, vat);
        Assert.Equal(990m, total);
    }

    [Fact]
    public void IsProtectedFromAutoDisable_SkipAutoDisable_AlwaysProtectsVip()
    {
        bool protectedVip = ClientVipPricing.IsProtectedFromAutoDisable(
            true,
            DateTime.Now.AddDays(-30),
            new CompanyVipPolicy(0m, 0, SkipAutoDisable: true),
            DateTime.Now);

        Assert.True(protectedVip);
    }

    [Fact]
    public void IsProtectedFromAutoDisable_GraceDays_ProtectsUntilGraceEnds()
    {
        DateTime expiration = new(2026, 8, 20, 0, 0, 0);
        CompanyVipPolicy policy = new(0m, 7, false);

        Assert.True(ClientVipPricing.IsProtectedFromAutoDisable(true, expiration, policy, expiration.AddDays(6)));
        Assert.False(ClientVipPricing.IsProtectedFromAutoDisable(true, expiration, policy, expiration.AddDays(7)));
        Assert.False(ClientVipPricing.IsProtectedFromAutoDisable(false, expiration, policy, expiration.AddDays(1)));
    }

    [Fact]
    public async Task GetCompanyPolicyAsync_UsesParentNetworkSettings()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network
        {
            Id = 1,
            Name = "Company",
            VipDiscountPercent = 15m,
            VipGraceDays = 5,
            VipSkipAutoDisable = true
        });
        db.Networks.Add(new Network
        {
            Id = 2,
            Name = "Branch",
            ParentNetworkId = 1
        });
        await db.SaveChangesAsync();

        ClientVipPolicyService sut = new(db);
        CompanyVipPolicy policy = await sut.GetCompanyPolicyAsync(2);

        Assert.Equal(15m, policy.DiscountPercent);
        Assert.Equal(5, policy.GraceDays);
        Assert.True(policy.SkipAutoDisable);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
