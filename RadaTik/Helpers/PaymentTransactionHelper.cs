using RadaTik.Models;

namespace RadaTik.Helpers;

public static class PaymentTransactionHelper
{
    /// <summary>تهيئة عملية تحصيل بليرة واحدة (السلوك القديم).</summary>
    public static void ApplySingleCurrencySyp(PaymentTransaction tx, decimal amount, PricingCurrency accountCurrency)
    {
        tx.Amount = amount;
        tx.PaymentAmount = amount;
        tx.PaymentCurrency = PricingCurrency.SYP_New;
        tx.AccountAmount = amount;
        tx.AccountCurrency = accountCurrency;
        tx.ExchangeRate = null;
    }

    public static void ApplyDualCurrencyCollection(
        PaymentTransaction tx,
        decimal paymentAmountSyp,
        decimal accountAmount,
        PricingCurrency accountCurrency,
        decimal exchangeRate)
    {
        tx.PaymentAmount = paymentAmountSyp;
        tx.PaymentCurrency = PricingCurrency.SYP_New;
        tx.AccountAmount = accountAmount;
        tx.AccountCurrency = accountCurrency;
        tx.ExchangeRate = exchangeRate;
        tx.Amount = paymentAmountSyp;
    }

    /// <summary>خصم من محفظة المشترك بنفس عملة الحساب (تجديد ذاتي، بدون تحصيل نقدي).</summary>
    public static void ApplyAccountWalletDebit(PaymentTransaction tx, decimal amount, PricingCurrency accountCurrency)
    {
        tx.PaymentAmount = amount;
        tx.PaymentCurrency = accountCurrency;
        tx.AccountAmount = amount;
        tx.AccountCurrency = accountCurrency;
        tx.ExchangeRate = null;
        tx.Amount = amount;
    }
}
