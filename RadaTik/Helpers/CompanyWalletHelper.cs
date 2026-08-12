using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>
/// محفظتا الشركة: Balance = ل.س.ج (تحصيل/منصة)، BalanceUsd = دولار (فواتير مواد واشتراكات USD).
/// </summary>
public static class CompanyWalletHelper
{
    public static decimal GetBalance(Network network, PricingCurrency currency) =>
        currency == PricingCurrency.USD ? network.BalanceUsd : network.Balance;

    public static void SetBalance(Network network, PricingCurrency currency, decimal value)
    {
        if (currency == PricingCurrency.USD)
        {
            network.BalanceUsd = value;
        }
        else
        {
            network.Balance = value;
        }
    }

    public static void ApplyDelta(Network network, PricingCurrency currency, decimal signedDelta)
    {
        if (currency == PricingCurrency.USD)
        {
            network.BalanceUsd += signedDelta;
        }
        else
        {
            network.Balance += signedDelta;
        }
    }

    public static bool HasSufficientBalance(Network network, PricingCurrency currency, decimal amount) =>
        amount <= 0m || GetBalance(network, currency) >= amount;

    public static string FormatInsufficientBalanceMessage(
        Network network,
        PricingCurrency currency,
        decimal required,
        decimal? available = null)
    {
        decimal avail = available ?? GetBalance(network, currency);
        return $"رصيد المحفظة ({CurrencyHelper.GetSymbol(currency)}) غير كافٍ. المطلوب: {CurrencyHelper.FormatAmount(required, currency)} — المتاح: {CurrencyHelper.FormatAmount(avail, currency)}";
    }
}
