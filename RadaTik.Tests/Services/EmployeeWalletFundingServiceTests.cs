using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;
using RadaTik.Services;
using RadaTik.Services.PricingPolicies;
using RadaTik.Security;
using Xunit;

namespace RadaTik.Tests.Services;

public class EmployeeWalletFundingServiceTests
{
    [Fact]
    public async Task DirectTopUp_DeductsCompanyWalletAndCreditsEmployee()
    {
        await using ApplicationDbContext context = CreateContext();
        Network company = new()
        {
            Name = "Co",
            Balance = 50_000m,
            CreationDate = DateTime.UtcNow
        };
        context.Networks.Add(company);
        await context.SaveChangesAsync();

        PayrollEmployee employee = new()
        {
            CompanyNetworkId = company.Id,
            FullName = "Ali",
            MonthlySalary = 10_000m,
            IsActive = true
        };
        context.PayrollEmployees.Add(employee);
        await context.SaveChangesAsync();

        EmployeeWalletFundingService funding = CreateFundingService(context);
        EmployeeWalletFundingResult result = await funding.FundAsync(
            employee,
            company.Id,
            5_000m,
            "admin",
            EmployeeWalletTransactionSource.DirectTopUpByManager,
            null,
            "test");

        Assert.True(result.Success);
        Assert.Equal(5_000m, result.NewEmployeeBalance);

        await context.Entry(company).ReloadAsync();
        await context.Entry(employee).ReloadAsync();
        Assert.Equal(45_000m, company.Balance);
        Assert.Equal(5_000m, employee.WalletBalance);

        int walletTxCount = await context.EmployeeWalletTransactions.CountAsync();
        Assert.Equal(1, walletTxCount);
        int networkTxCount = await context.NetworkWalletTransactions.CountAsync();
        Assert.Equal(1, networkTxCount);
    }

    [Fact]
    public async Task DirectTopUp_WithPercentCommission_DeductsExtraFromCompany()
    {
        await using ApplicationDbContext context = CreateContext();
        Network company = new()
        {
            Name = "Co",
            Balance = 50_000m,
            CreationDate = DateTime.UtcNow
        };
        context.Networks.Add(company);
        context.FeaturePricings.Add(new()
        {
            FeatureKey = FeatureKeys.PayrollWalletTransferCommission,
            BillingPeriod = PricingBillingPeriod.OneTime,
            ChargeUnit = PricingChargeUnit.PercentOfCollectedAmount,
            AmountSYP = 2m,
            IsActive = true
        });
        await context.SaveChangesAsync();

        PayrollEmployee employee = new()
        {
            CompanyNetworkId = company.Id,
            FullName = "Sara",
            MonthlySalary = 8_000m,
            IsActive = true
        };
        context.PayrollEmployees.Add(employee);
        await context.SaveChangesAsync();

        EmployeeWalletFundingService funding = CreateFundingService(context);
        EmployeeWalletFundingResult result = await funding.FundAsync(
            employee,
            company.Id,
            10_000m,
            "admin",
            EmployeeWalletTransactionSource.DirectTopUpByManager,
            null,
            null);

        Assert.True(result.Success);
        Assert.Equal(200m, result.CommissionCharged);

        await context.Entry(company).ReloadAsync();
        Assert.Equal(39_800m, company.Balance);
    }

    private static ApplicationDbContext CreateContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static EmployeeWalletFundingService CreateFundingService(ApplicationDbContext context)
    {
        ICollectionCommissionPricingResolver resolver = new CollectionCommissionPricingResolver(
            [new PercentageCollectionCommissionPricingStrategy()]);
        EmployeeWalletTopUpCommissionService commission = new(context, resolver);
        return new EmployeeWalletFundingService(context, commission);
    }
}
