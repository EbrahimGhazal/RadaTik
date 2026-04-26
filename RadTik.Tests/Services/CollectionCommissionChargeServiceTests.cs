using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.PricingPolicies;
using Xunit;

namespace RadTik.Tests.Services;

public class CollectionCommissionChargeServiceTests
{
    [Fact]
    public void Resolver_ComputesPercentageFeeWithCeiling()
    {
        var resolver = new CollectionCommissionPricingResolver(
            new ICollectionCommissionPricingStrategy[] { new PercentageCollectionCommissionPricingStrategy() });
        var pricing = new FeaturePricing
        {
            ChargeUnit = PricingChargeUnit.PercentOfCollectedAmount,
            AmountSYP = 2.5m
        };

        var result = resolver.Resolve(pricing, 101m);

        Assert.True(result.IsSupported);
        Assert.Equal(2.5m, result.PercentValue);
        Assert.Equal(3m, result.FeeAmountSyp);
    }

    [Fact]
    public async Task ChargeAfterPaymentRecordedAsync_Success_ChargesCompanyWallet()
    {
        await using var db = CreateDb();
        SeedScenario(db, companyBalance: 500m, paymentAmount: 200m, percent: 10m);

        var service = CreateService(db);
        var result = await service.ChargeAfterPaymentRecordedAsync(paymentTransactionId: 100, paymentAmountSyp: 200m);

        Assert.True(result.Success);
        Assert.Equal(20m, result.FeeChargedSyp);
        Assert.False(result.SkippedNoPricing);

        var company = await db.Networks.FirstAsync(n => n.Id == 1);
        var tx = await db.NetworkWalletTransactions.FirstOrDefaultAsync();
        Assert.Equal(480m, company.Balance);
        Assert.NotNull(tx);
        Assert.Equal(NetworkWalletTransactionType.CollectionCommission, tx!.Type);
        Assert.Equal(-20m, tx.SignedAmount);
    }

    [Fact]
    public async Task ChargeAfterPaymentRecordedAsync_InsufficientBalance_ReturnsFailure()
    {
        await using var db = CreateDb();
        SeedScenario(db, companyBalance: 5m, paymentAmount: 200m, percent: 10m);

        var service = CreateService(db);
        var result = await service.ChargeAfterPaymentRecordedAsync(paymentTransactionId: 100, paymentAmountSyp: 200m);

        Assert.False(result.Success);
        Assert.Contains("رصيد محفظة الشركة", result.ErrorMessage ?? string.Empty);

        var company = await db.Networks.FirstAsync(n => n.Id == 1);
        var tx = await db.NetworkWalletTransactions.FirstOrDefaultAsync();
        Assert.Equal(5m, company.Balance);
        Assert.Null(tx);
    }

    private static CollectionCommissionChargeService CreateService(ApplicationDbContext db)
    {
        var resolver = new CollectionCommissionPricingResolver(
            new ICollectionCommissionPricingStrategy[] { new PercentageCollectionCommissionPricingStrategy() });
        return new CollectionCommissionChargeService(db, NullLogger<CollectionCommissionChargeService>.Instance, resolver);
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedScenario(ApplicationDbContext db, decimal companyBalance, decimal paymentAmount, decimal percent)
    {
        db.Networks.AddRange(
            new Network { Id = 1, Name = "Company", Balance = companyBalance },
            new Network { Id = 2, Name = "Branch", ParentNetworkId = 1, Balance = 0m });

        db.FeaturePricings.Add(new FeaturePricing
        {
            Id = 10,
            FeatureKey = FeatureKeys.CollectionCommission,
            BillingPeriod = PricingBillingPeriod.OneTime,
            ChargeUnit = PricingChargeUnit.PercentOfCollectedAmount,
            AmountSYP = percent,
            AmountUSD = 0m,
            Currency = PricingCurrency.SYP_New,
            IsActive = true
        });

        db.Clients.Add(new Client
        {
            Id = 50,
            Name = "Client",
            SID = "SID-1",
            UserName = "client-1",
            Password = "x",
            PhoneNumber = "0999000000",
            ProfileId = 1,
            NetworkId = 2,
            IsActive = true,
            CreatedDate = DateTime.Now
        });

        db.PaymentTransactions.Add(new PaymentTransaction
        {
            Id = 100,
            ClientId = 50,
            NetworkId = 2,
            Amount = paymentAmount,
            PaymentDate = DateTime.Now,
            ReceivedByUserId = "collector-1",
            OperationType = "ReceivePayment",
            ReferenceNumber = "REF-100",
            PreviousClientBalance = 0m,
            NewClientBalance = 0m,
            PreviousPointBalance = 0m,
            NewPointBalance = 0m
        });

        db.SaveChanges();
    }
}
