using System.Globalization;
using Microsoft.AspNetCore.Mvc.Rendering;
using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>عرض العملات وتحويل USD ↔ ل.س.ج عند التحصيل.</summary>
public static class CurrencyHelper
{
    public static bool IsSyrian(PricingCurrency currency) =>
        currency is PricingCurrency.SYP_New or PricingCurrency.SYP_Old;

    public static bool RequiresExchangeAtCollection(PricingCurrency accountCurrency) =>
        accountCurrency == PricingCurrency.USD;

    public static string GetSymbol(PricingCurrency currency) => currency switch
    {
        PricingCurrency.USD => "$",
        PricingCurrency.SYP_Old => "ل.س",
        _ => "ل.س.ج"
    };

    public static string FormatAmount(decimal amount, PricingCurrency currency, int decimals = 2)
    {
        string formatted = amount.ToString($"N{decimals}", CultureInfo.InvariantCulture);
        return $"{formatted} {GetSymbol(currency)}";
    }

    public static string FormatAmount(decimal? amount, PricingCurrency currency, int decimals = 2) =>
        amount.HasValue ? FormatAmount(amount.Value, currency, decimals) : "—";

    /// <summary>1 USD = rate ل.س.ج جديدة.</summary>
    public static decimal ConvertSypToAccountAmount(decimal paymentAmountSyp, decimal usdToSypRate, PricingCurrency accountCurrency)
    {
        if (accountCurrency != PricingCurrency.USD)
        {
            return paymentAmountSyp;
        }

        if (usdToSypRate <= 0m)
        {
            throw new InvalidOperationException("سعر صرف الدولار غير صالح.");
        }

        return Math.Round(paymentAmountSyp / usdToSypRate, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ConvertAccountToSyp(decimal accountAmount, decimal usdToSypRate, PricingCurrency accountCurrency)
    {
        if (accountCurrency != PricingCurrency.USD)
        {
            return accountAmount;
        }

        if (usdToSypRate <= 0m)
        {
            throw new InvalidOperationException("سعر صرف الدولار غير صالح.");
        }

        return Math.Round(accountAmount * usdToSypRate, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>عملات مدير الشركة: فواتير مواد وحسابات مشتركين.</summary>
    public static IReadOnlyList<SelectListItem> GetCompanyOperatingCurrencySelectItems(PricingCurrency? selected = null)
    {
        PricingCurrency sel = selected ?? PricingCurrency.SYP_New;
        return
        [
            new SelectListItem { Value = ((int)PricingCurrency.SYP_New).ToString(), Text = "ل.س.ج (ليرة جديدة)", Selected = sel == PricingCurrency.SYP_New },
            new SelectListItem { Value = ((int)PricingCurrency.USD).ToString(), Text = "$ (دولار)", Selected = sel == PricingCurrency.USD }
        ];
    }
}
