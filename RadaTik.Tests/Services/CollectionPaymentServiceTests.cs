using RadaTik.Models;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public class CollectionPaymentServiceTests
{
    private readonly ICollectionPaymentService _service = new CollectionPaymentService();

    [Fact]
    public void Validate_SypAccount_UsesSameAmountForClientAndPoint()
    {
        Client client = new() { AccountCurrency = PricingCurrency.SYP_New };
        CollectionPaymentApplyResult result = _service.ValidateAndCompute(client, 5000m, PricingCurrency.SYP_New, null, null);

        Assert.True(result.Success);
        Assert.Equal(5000m, result.ClientBalanceDelta);
        Assert.Equal(5000m, result.PointBalanceDelta);
        Assert.Null(result.ExchangeRateUsed);
    }

    [Fact]
    public void Validate_UsdAccount_ConvertsPaymentToAccountAmount()
    {
        Client client = new() { AccountCurrency = PricingCurrency.USD };
        CollectionPaymentApplyResult result = _service.ValidateAndCompute(client, 14000m, PricingCurrency.SYP_New, 140m, null);

        Assert.True(result.Success);
        Assert.Equal(100m, result.AccountAmountApplied);
        Assert.Equal(14000m, result.PaymentAmountApplied);
        Assert.Equal(100m, result.ClientBalanceDelta);
        Assert.Equal(14000m, result.PointBalanceDelta);
        Assert.Equal(140m, result.ExchangeRateUsed);
    }

    [Fact]
    public void Validate_UsdAccount_RequiresExchangeRate()
    {
        Client client = new() { AccountCurrency = PricingCurrency.USD };
        CollectionPaymentApplyResult result = _service.ValidateAndCompute(client, 14000m, PricingCurrency.SYP_New, null, null);

        Assert.False(result.Success);
    }

    [Fact]
    public void QuoteRenewal_UsdAccount_ConvertsToPointChargeSyp()
    {
        CollectionRenewalQuote quote = _service.QuoteAccountCharge(PricingCurrency.USD, 100m, 140m);

        Assert.True(quote.Success);
        Assert.Equal(100m, quote.AmountDueAccount);
        Assert.Equal(14000m, quote.PointChargeSyp);
    }

    [Fact]
    public void QuoteRenewal_SypAccount_NoConversion()
    {
        CollectionRenewalQuote quote = _service.QuoteAccountCharge(PricingCurrency.SYP_New, 5000m, null);

        Assert.True(quote.Success);
        Assert.Equal(5000m, quote.AmountDueAccount);
        Assert.Equal(5000m, quote.PointChargeSyp);
    }
}
