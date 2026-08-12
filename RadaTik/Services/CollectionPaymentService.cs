using RadaTik.Domain.ValueObjects;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class CollectionPaymentApplyResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal ClientBalanceDelta { get; init; }
    public decimal PointBalanceDelta { get; init; }
    public decimal AccountAmountApplied { get; init; }
    public decimal PaymentAmountApplied { get; init; }
    public decimal? ExchangeRateUsed { get; init; }

    public static CollectionPaymentApplyResult Ok(
        decimal clientDelta,
        decimal pointDelta,
        decimal accountAmount,
        decimal paymentAmount,
        decimal? rate) =>
        new()
        {
            Success = true,
            ClientBalanceDelta = clientDelta,
            PointBalanceDelta = pointDelta,
            AccountAmountApplied = accountAmount,
            PaymentAmountApplied = paymentAmount,
            ExchangeRateUsed = rate
        };

    public static CollectionPaymentApplyResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

public interface ICollectionPaymentService
{
    CollectionPaymentApplyResult ValidateAndCompute(
        Client client,
        decimal paymentAmount,
        PricingCurrency paymentCurrency,
        decimal? exchangeRate,
        decimal? accountAmountOverride);

    void FillPaymentTransaction(
        PaymentTransaction payment,
        CollectionPaymentApplyResult computed,
        PricingCurrency accountCurrency);

    CollectionRenewalQuote QuoteAccountCharge(
        PricingCurrency accountCurrency,
        decimal amountDueAccount,
        decimal? exchangeRate);

    void FillRenewalPaymentTransaction(
        PaymentTransaction payment,
        CollectionRenewalQuote quote);
}

/// <summary>تطبيق تحصيل نقدي على رصيد العميل ونقطة التحصيل مع دعم حساب USD.</summary>
public sealed class CollectionPaymentService : ICollectionPaymentService
{
    public CollectionPaymentApplyResult ValidateAndCompute(
        Client client,
        decimal paymentAmount,
        PricingCurrency paymentCurrency,
        decimal? exchangeRate,
        decimal? accountAmountOverride)
    {
        if (!MoneyAmount.TryCreate(paymentAmount, paymentCurrency).IsSuccess)
        {
            return CollectionPaymentApplyResult.Fail("المبلغ المستلم يجب أن يكون أكبر من صفر.");
        }

        if (!CurrencyHelper.IsSyrian(paymentCurrency))
        {
            return CollectionPaymentApplyResult.Fail("نقطة التحصيل تدعم حالياً استلام الليرة السورية فقط.");
        }

        if (!CurrencyHelper.RequiresExchangeAtCollection(client.AccountCurrency))
        {
            return CollectionPaymentApplyResult.Ok(
                clientDelta: paymentAmount,
                pointDelta: paymentAmount,
                accountAmount: paymentAmount,
                paymentAmount: paymentAmount,
                rate: null);
        }

        if (!exchangeRate.HasValue || exchangeRate.Value <= 0m)
        {
            return CollectionPaymentApplyResult.Fail("سعر صرف الدولار مطلوب لتحصيل حساب بالدولار.");
        }

        decimal accountAmount = accountAmountOverride.HasValue && accountAmountOverride.Value > 0m
            ? accountAmountOverride.Value
            : CurrencyHelper.ConvertSypToAccountAmount(paymentAmount, exchangeRate.Value, PricingCurrency.USD);

        if (accountAmount <= 0m)
        {
            return CollectionPaymentApplyResult.Fail("المبلغ المحوّل على حساب العميل يجب أن يكون أكبر من صفر.");
        }

        return CollectionPaymentApplyResult.Ok(
            clientDelta: accountAmount,
            pointDelta: paymentAmount,
            accountAmount: accountAmount,
            paymentAmount: paymentAmount,
            rate: exchangeRate.Value);
    }

    public void FillPaymentTransaction(
        PaymentTransaction payment,
        CollectionPaymentApplyResult computed,
        PricingCurrency accountCurrency)
    {
        if (!CurrencyHelper.RequiresExchangeAtCollection(accountCurrency))
        {
            PaymentTransactionHelper.ApplySingleCurrencySyp(payment, computed.PaymentAmountApplied, accountCurrency);
            return;
        }

        PaymentTransactionHelper.ApplyDualCurrencyCollection(
            payment,
            computed.PaymentAmountApplied,
            computed.AccountAmountApplied,
            accountCurrency,
            computed.ExchangeRateUsed!.Value);
    }

    /// <summary>احتساب ما تخصمه نقطة التحصيل (ل.س) مقابل استحقاق بالعملة المحاسبية للمشترك.</summary>
    public CollectionRenewalQuote QuoteAccountCharge(
        PricingCurrency accountCurrency,
        decimal amountDueAccount,
        decimal? exchangeRate)
    {
        if (amountDueAccount < 0.01m)
        {
            return CollectionRenewalQuote.Fail("المبلغ المستحق غير صالح.");
        }

        if (!CurrencyHelper.RequiresExchangeAtCollection(accountCurrency))
        {
            return CollectionRenewalQuote.Ok(
                amountDueAccount,
                amountDueAccount,
                accountCurrency,
                null);
        }

        if (!exchangeRate.HasValue || exchangeRate.Value <= 0m)
        {
            return CollectionRenewalQuote.Fail("سعر صرف الدولار غير محدد للشركة. راجع مدير الشركة.");
        }

        decimal pointChargeSyp = CurrencyHelper.ConvertAccountToSyp(
            amountDueAccount,
            exchangeRate.Value,
            PricingCurrency.USD);

        return CollectionRenewalQuote.Ok(
            amountDueAccount,
            pointChargeSyp,
            accountCurrency,
            exchangeRate.Value);
    }

    public void FillRenewalPaymentTransaction(
        PaymentTransaction payment,
        CollectionRenewalQuote quote)
    {
        if (!quote.Success)
        {
            throw new InvalidOperationException(quote.ErrorMessage ?? "عرض التجديد غير صالح.");
        }

        if (!CurrencyHelper.RequiresExchangeAtCollection(quote.AccountCurrency))
        {
            PaymentTransactionHelper.ApplySingleCurrencySyp(
                payment,
                quote.PointChargeSyp,
                quote.AccountCurrency);
            return;
        }

        PaymentTransactionHelper.ApplyDualCurrencyCollection(
            payment,
            quote.PointChargeSyp,
            quote.AmountDueAccount,
            quote.AccountCurrency,
            quote.ExchangeRate!.Value);
    }
}

public sealed class CollectionRenewalQuote
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public decimal AmountDueAccount { get; init; }
    public decimal PointChargeSyp { get; init; }
    public PricingCurrency AccountCurrency { get; init; }
    public decimal? ExchangeRate { get; init; }

    public static CollectionRenewalQuote Ok(
        decimal amountDueAccount,
        decimal pointChargeSyp,
        PricingCurrency accountCurrency,
        decimal? exchangeRate) =>
        new()
        {
            Success = true,
            AmountDueAccount = amountDueAccount,
            PointChargeSyp = pointChargeSyp,
            AccountCurrency = accountCurrency,
            ExchangeRate = exchangeRate
        };

    public static CollectionRenewalQuote Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
