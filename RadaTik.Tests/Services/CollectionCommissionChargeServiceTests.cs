using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.PricingPolicies;
using Xunit;

namespace RadaTik.Tests.Services;

public class CollectionCommissionChargeServiceTests
{
    [Fact]
    public void Resolver_ComputesPercentageFeeWithCeiling()
    {
        CollectionCommissionPricingResolver resolver = new CollectionCommissionPricingResolver(
            new ICollectionCommissionPricingStrategy[] { new PercentageCollectionCommissionPricingStrategy() });
        FeaturePricing pricing = new FeaturePricing
        {
            ChargeUnit = PricingChargeUnit.PercentOfCollectedAmount,
            AmountSYP = 2.5m
        };

        CollectionCommissionPricingComputation result = resolver.Resolve(pricing, 101m);

        Assert.True(result.IsSupported);
        Assert.Equal(2.5m, result.PercentValue);
        Assert.Equal(3m, result.FeeAmountSyp);
    }

    [Fact]
    public async Task ChargeAfterPaymentRecordedAsync_Success_ChargesCompanyWallet()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedScenario(db, companyBalance: 500m, paymentAmount: 200m, percent: 10m);

        CollectionCommissionChargeService service = CreateService(db);
        CollectionCommissionChargeResult result = await service.ChargeAfterPaymentRecordedAsync(paymentTransactionId: 100, paymentAmountSyp: 200m);

        Assert.True(result.Success);
        Assert.Equal(20m, result.FeeChargedSyp);
        Assert.False(result.SkippedNoPricing);

        Network company = await db.Networks.FirstAsync(n => n.Id == 1);
        List<NetworkWalletTransaction> txs = await db.NetworkWalletTransactions.OrderBy(t => t.Id).ToListAsync();
        Assert.Equal(680m, company.Balance);
        Assert.Equal(2, txs.Count);
        Assert.Equal(NetworkWalletTransactionType.SubscriptionCollectedRevenue, txs[0].Type);
        Assert.Equal(200m, txs[0].SignedAmount);
        Assert.Equal(NetworkWalletTransactionType.CollectionCommission, txs[1].Type);
        Assert.Equal(-20m, txs[1].SignedAmount);
    }

    [Fact]
    public async Task ChargeAfterPaymentRecordedAsync_InsufficientBalance_StillSettlesGrossAndFee()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedScenario(db, companyBalance: 5m, paymentAmount: 200m, percent: 10m);

        CollectionCommissionChargeService service = CreateService(db);
        CollectionCommissionChargeResult result = await service.ChargeAfterPaymentRecordedAsync(paymentTransactionId: 100, paymentAmountSyp: 200m);

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        Network company = await db.Networks.FirstAsync(n => n.Id == 1);
        List<NetworkWalletTransaction> txs = await db.NetworkWalletTransactions.OrderBy(t => t.Id).ToListAsync();
        Assert.Equal(185m, company.Balance);
        Assert.Equal(2, txs.Count);
    }

    private static CollectionCommissionChargeService CreateService(ApplicationDbContext db)
    {
        CollectionCommissionPricingResolver resolver = new CollectionCommissionPricingResolver(
            new ICollectionCommissionPricingStrategy[] { new PercentageCollectionCommissionPricingStrategy() });
        return new CollectionCommissionChargeService(db, NullLogger<CollectionCommissionChargeService>.Instance, resolver);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
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

        PaymentTransaction payment = new PaymentTransaction
        {
            Id = 100,
            ClientId = 50,
            NetworkId = 2,
            PaymentDate = DateTime.Now,
            ReceivedByUserId = "collector-1",
            OperationType = "ReceivePayment",
            ReferenceNumber = "REF-100",
            PreviousClientBalance = 0m,
            NewClientBalance = 0m,
            PreviousPointBalance = 0m,
            NewPointBalance = 0m
        };
        PaymentTransactionHelper.ApplySingleCurrencySyp(payment, paymentAmount, PricingCurrency.SYP_New);
        db.PaymentTransactions.Add(payment);

        db.SaveChanges();
    }
}
