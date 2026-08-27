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
    public void ApplyPackageDiscount_ClientPercentOverridesCompanyPolicy()
    {
        decimal result = ClientVipPricing.ApplyPackageDiscount(
            1000m,
            isVip: true,
            new CompanyVipPolicy(10m, 0, false),
            ClientVipBenefitKind.Discount,
            25m);

        Assert.Equal(750m, result);
    }

    [Fact]
    public void ApplyPackageDiscount_PermanentlyFree_ReturnsZero()
    {
        decimal result = ClientVipPricing.ApplyPackageDiscount(
            1000m,
            isVip: true,
            new CompanyVipPolicy(10m, 0, false),
            ClientVipBenefitKind.PermanentlyFree,
            25m);

        Assert.Equal(0m, result);
    }

    [Fact]
    public void IsProtectedFromAutoDisable_PermanentlyFree_AlwaysProtects()
    {
        bool protectedVip = ClientVipPricing.IsProtectedFromAutoDisable(
            true,
            DateTime.Now.AddDays(-30),
            new CompanyVipPolicy(0m, 0, SkipAutoDisable: false),
            DateTime.Now,
            ClientVipBenefitKind.PermanentlyFree);

        Assert.True(protectedVip);
    }

    [Fact]
    public void Apply_ClearsBenefitWhenVipRemoved()
    {
        Client client = new()
        {
            IsVip = true,
            VipNote = "شريك",
            VipBenefitKind = ClientVipBenefitKind.Discount,
            VipDiscountPercent = 40m,
            VipSince = DateTime.Now.AddDays(-1)
        };

        ClientVipAssignment.Apply(client, false, "شريك", DateTime.Now);

        Assert.False(client.IsVip);
        Assert.Equal(ClientVipBenefitKind.None, client.VipBenefitKind);
        Assert.Equal(0m, client.VipDiscountPercent);
        Assert.Null(client.VipNote);
        Assert.Null(client.VipSince);
    }

    [Fact]
    public void Apply_ManagerSetsCustomDiscountPercent()
    {
        Client client = new();
        ClientVipAssignment.Apply(
            client,
            true,
            "موظف",
            DateTime.Now,
            ClientVipBenefitKind.Discount,
            35m);

        Assert.True(client.IsVip);
        Assert.Equal(ClientVipBenefitKind.Discount, client.VipBenefitKind);
        Assert.Equal(35m, client.VipDiscountPercent);
    }

    [Fact]
    public void BadgeText_ShowsPercentWhenSet()
    {
        Client client = new()
        {
            IsVip = true,
            VipBenefitKind = ClientVipBenefitKind.Discount,
            VipDiscountPercent = 20m
        };

        Assert.Equal("VIP · حسم 20%", ClientVipBenefitDisplay.BadgeText(client));
        Assert.Equal("VIP · مجاني", ClientVipBenefitDisplay.BadgeText(new Client
        {
            IsVip = true,
            VipBenefitKind = ClientVipBenefitKind.PermanentlyFree
        }));
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
