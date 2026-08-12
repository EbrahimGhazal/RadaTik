using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>مساعد الصندوق النقدي — رصيد ل.س.ج (<see cref="CashBox.Balance"/>) ورصيد $ (<see cref="CashBox.BalanceUsd"/>).</summary>
public static class CashBoxHelper
{
    public static PricingCurrency NormalizeOperatingCurrency(PricingCurrency currency) =>
        currency == PricingCurrency.USD ? PricingCurrency.USD : PricingCurrency.SYP_New;

    public static decimal GetBalance(CashBox box, PricingCurrency currency) =>
        NormalizeOperatingCurrency(currency) == PricingCurrency.USD ? box.BalanceUsd : box.Balance;

    public static void ApplyDelta(CashBox box, PricingCurrency currency, decimal signedDelta)
    {
        if (NormalizeOperatingCurrency(currency) == PricingCurrency.USD)
        {
            box.BalanceUsd += signedDelta;
        }
        else
        {
            box.Balance += signedDelta;
        }

        box.UpdatedAt = DateTime.Now;
    }

    public static bool HasSufficientBalance(CashBox box, PricingCurrency currency, decimal amount) =>
        amount <= 0m || GetBalance(box, currency) >= amount;

    public static string FormatInsufficientBalanceMessage(
        CashBox box,
        PricingCurrency currency,
        decimal required)
    {
        decimal available = GetBalance(box, currency);
        return
            $"رصيد الصندوق غير كافٍ. المتوفر: {CurrencyHelper.FormatAmount(available, NormalizeOperatingCurrency(currency))} — المطلوب: {CurrencyHelper.FormatAmount(required, NormalizeOperatingCurrency(currency))}";
    }

    public static PricingCurrency GetOppositeOperatingCurrency(PricingCurrency from) =>
        NormalizeOperatingCurrency(from) == PricingCurrency.USD
            ? PricingCurrency.SYP_New
            : PricingCurrency.USD;

    /// <summary>تحويل داخل الصندوق: 1 USD = <paramref name="usdToSypRate"/> ل.س.ج.</summary>
    public static decimal ComputeExchangeTargetAmount(
        decimal sourceAmount,
        PricingCurrency fromCurrency,
        decimal usdToSypRate)
    {
        if (sourceAmount <= 0m)
        {
            throw new InvalidOperationException("مبلغ التحويل يجب أن يكون أكبر من صفر.");
        }

        if (usdToSypRate <= 0m)
        {
            throw new InvalidOperationException("سعر الصرف يجب أن يكون أكبر من صفر.");
        }

        PricingCurrency from = NormalizeOperatingCurrency(fromCurrency);
        return from == PricingCurrency.USD
            ? Math.Round(sourceAmount * usdToSypRate, 2, MidpointRounding.AwayFromZero)
            : Math.Round(sourceAmount / usdToSypRate, 2, MidpointRounding.AwayFromZero);
    }

    public static async Task<CashBox?> GetOrCreateCashBoxAsync(
        ApplicationDbContext context,
        CashBoxOwnerType ownerType,
        int ownerId)
    {
        CashBox? box = await context.CashBoxes
            .FirstOrDefaultAsync(c => c.OwnerType == ownerType && c.OwnerId == ownerId);
        if (box != null)
        {
            return box;
        }

        box = new CashBox
        {
            OwnerType = ownerType,
            OwnerId = ownerId,
            Balance = 0m,
            BalanceUsd = 0m,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        context.CashBoxes.Add(box);
        await context.SaveChangesAsync();
        return box;
    }
}
