using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class CurrencyHelperTests
{
    [Theory]
    [InlineData(PricingCurrency.SYP_New, true)]
    [InlineData(PricingCurrency.SYP_Old, true)]
    [InlineData(PricingCurrency.USD, false)]
    public void IsSyrian_RecognizesSyrianCurrencies(PricingCurrency currency, bool expected)
    {
        Assert.Equal(expected, CurrencyHelper.IsSyrian(currency));
    }

    [Fact]
    public void ConvertSypToAccountAmount_UsdDividesByRate()
    {
        decimal result = CurrencyHelper.ConvertSypToAccountAmount(15000m, 15000m, PricingCurrency.USD);
        Assert.Equal(1.00m, result);
    }

    [Fact]
    public void ConvertSypToAccountAmount_SypUnchanged()
    {
        decimal result = CurrencyHelper.ConvertSypToAccountAmount(5000m, 15000m, PricingCurrency.SYP_New);
        Assert.Equal(5000m, result);
    }

    [Fact]
    public void ConvertSypToAccountAmount_InvalidRateThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CurrencyHelper.ConvertSypToAccountAmount(100m, 0m, PricingCurrency.USD));
    }
}
