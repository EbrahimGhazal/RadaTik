using Microsoft.AspNetCore.Mvc.Rendering;
using RadaTik.Models;

namespace RadaTik.Helpers;

public interface ICurrencyHelper
{
    bool IsSyrian(PricingCurrency currency);

    bool RequiresExchangeAtCollection(PricingCurrency accountCurrency);

    string GetSymbol(PricingCurrency currency);

    string FormatAmount(decimal amount, PricingCurrency currency, int decimals = 2);

    string FormatAmount(decimal? amount, PricingCurrency currency, int decimals = 2);

    decimal ConvertSypToAccountAmount(decimal paymentAmountSyp, decimal usdToSypRate, PricingCurrency accountCurrency);

    decimal ConvertAccountToSyp(decimal accountAmount, decimal usdToSypRate, PricingCurrency accountCurrency);

    IReadOnlyList<SelectListItem> GetCompanyOperatingCurrencySelectItems(PricingCurrency? selected = null);
}

public sealed class CurrencyHelperAdapter : ICurrencyHelper
{
    public bool IsSyrian(PricingCurrency currency) => CurrencyHelper.IsSyrian(currency);

    public bool RequiresExchangeAtCollection(PricingCurrency accountCurrency) =>
        CurrencyHelper.RequiresExchangeAtCollection(accountCurrency);

    public string GetSymbol(PricingCurrency currency) => CurrencyHelper.GetSymbol(currency);

    public string FormatAmount(decimal amount, PricingCurrency currency, int decimals = 2) =>
        CurrencyHelper.FormatAmount(amount, currency, decimals);

    public string FormatAmount(decimal? amount, PricingCurrency currency, int decimals = 2) =>
        CurrencyHelper.FormatAmount(amount, currency, decimals);

    public decimal ConvertSypToAccountAmount(decimal paymentAmountSyp, decimal usdToSypRate, PricingCurrency accountCurrency) =>
        CurrencyHelper.ConvertSypToAccountAmount(paymentAmountSyp, usdToSypRate, accountCurrency);

    public decimal ConvertAccountToSyp(decimal accountAmount, decimal usdToSypRate, PricingCurrency accountCurrency) =>
        CurrencyHelper.ConvertAccountToSyp(accountAmount, usdToSypRate, accountCurrency);

    public IReadOnlyList<SelectListItem> GetCompanyOperatingCurrencySelectItems(PricingCurrency? selected = null) =>
        CurrencyHelper.GetCompanyOperatingCurrencySelectItems(selected);
}
