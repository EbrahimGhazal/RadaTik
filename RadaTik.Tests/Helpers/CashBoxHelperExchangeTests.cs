using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class CashBoxHelperExchangeTests
{
    [Fact]
    public void ComputeExchangeTargetAmount_SypToUsd_DividesByRate()
    {
        decimal target = CashBoxHelper.ComputeExchangeTargetAmount(15_000m, PricingCurrency.SYP_New, 15_000m);
        Assert.Equal(1m, target);
    }

    [Fact]
    public void ComputeExchangeTargetAmount_UsdToSyp_MultipliesByRate()
    {
        decimal target = CashBoxHelper.ComputeExchangeTargetAmount(100m, PricingCurrency.USD, 15_000m);
        Assert.Equal(1_500_000m, target);
    }

    [Fact]
    public void GetOppositeOperatingCurrency_TogglesSypAndUsd()
    {
        Assert.Equal(PricingCurrency.USD, CashBoxHelper.GetOppositeOperatingCurrency(PricingCurrency.SYP_New));
        Assert.Equal(PricingCurrency.SYP_New, CashBoxHelper.GetOppositeOperatingCurrency(PricingCurrency.USD));
    }

    [Fact]
    public void HasSufficientBalance_IsFalseWhenAmountExceedsCash()
    {
        CashBox box = new() { Balance = 100m, BalanceUsd = 5m };
        Assert.False(CashBoxHelper.HasSufficientBalance(box, PricingCurrency.SYP_New, 150m));
        Assert.True(CashBoxHelper.HasSufficientBalance(box, PricingCurrency.USD, 5m));
        Assert.False(CashBoxHelper.HasSufficientBalance(box, PricingCurrency.USD, 5.01m));
    }
}
