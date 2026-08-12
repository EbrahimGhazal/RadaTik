using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class CashBoxExchangeResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? ExchangeId { get; init; }

    public static CashBoxExchangeResult Ok(int exchangeId) => new() { Success = true, ExchangeId = exchangeId };
    public static CashBoxExchangeResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>تحويل نقد بين ل.س.ج و $ داخل صندوق الشركة مع تسجيل سحب وإيداع وحركة تحويل.</summary>
public sealed class CashBoxExchangeService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<CashBoxExchangeResult> ExecuteExchangeAsync(
        int companyNetworkId,
        string userId,
        PricingCurrency fromCurrency,
        decimal sourceAmount,
        decimal exchangeRate,
        string? notes,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            return CashBoxExchangeResult.Fail("يجب تسجيل الدخول.");
        }

        PricingCurrency from = CashBoxHelper.NormalizeOperatingCurrency(fromCurrency);
        PricingCurrency to = CashBoxHelper.GetOppositeOperatingCurrency(from);

        if (sourceAmount < 0.01m)
        {
            return CashBoxExchangeResult.Fail("مبلغ التحويل يجب أن يكون أكبر من صفر.");
        }

        if (exchangeRate <= 0m)
        {
            return CashBoxExchangeResult.Fail("سعر الصرف يجب أن يكون أكبر من صفر.");
        }

        decimal targetAmount;
        try
        {
            targetAmount = CashBoxHelper.ComputeExchangeTargetAmount(sourceAmount, from, exchangeRate);
        }
        catch (InvalidOperationException ex)
        {
            return CashBoxExchangeResult.Fail(ex.Message);
        }

        if (targetAmount < 0.01m)
        {
            return CashBoxExchangeResult.Fail("المبلغ المحوّل بعد الصرف صغير جداً.");
        }

        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(
            _context, CashBoxOwnerType.Network, companyNetworkId);
        if (cashBox == null)
        {
            return CashBoxExchangeResult.Fail("تعذر الوصول للصندوق النقدي.");
        }

        if (!CashBoxHelper.HasSufficientBalance(cashBox, from, sourceAmount))
        {
            return CashBoxExchangeResult.Fail(
                CashBoxHelper.FormatInsufficientBalanceMessage(cashBox, from, sourceAmount));
        }

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync(ct);
        DateTime now = DateTime.Now;
        string rateLabel = $"1 $ = {exchangeRate:N2} ل.س.ج";

        decimal fromBefore = CashBoxHelper.GetBalance(cashBox, from);
        CashBoxHelper.ApplyDelta(cashBox, from, -sourceAmount);
        CashBoxWithdrawal withdrawal = new()
        {
            CashBoxId = cashBox.Id,
            Amount = sourceAmount,
            Currency = from,
            WithdrawnAt = now,
            WithdrawnByUserId = userId,
            Notes = $"تحويل عملة → {CurrencyHelper.GetSymbol(to)} ({rateLabel})",
            BalanceBefore = fromBefore,
            BalanceAfter = CashBoxHelper.GetBalance(cashBox, from)
        };
        _context.CashBoxWithdrawals.Add(withdrawal);

        decimal toBefore = CashBoxHelper.GetBalance(cashBox, to);
        CashBoxHelper.ApplyDelta(cashBox, to, targetAmount);
        CashBoxDeposit deposit = new()
        {
            CashBoxId = cashBox.Id,
            Amount = targetAmount,
            Currency = to,
            DepositedAt = now,
            DepositedByUserId = userId,
            Notes = $"تحويل عملة ← {CurrencyHelper.GetSymbol(from)} ({rateLabel})",
            BalanceBefore = toBefore,
            BalanceAfter = CashBoxHelper.GetBalance(cashBox, to)
        };
        _context.CashBoxDeposits.Add(deposit);
        await _context.SaveChangesAsync(ct);

        CashBoxCurrencyExchange exchange = new()
        {
            CashBoxId = cashBox.Id,
            FromCurrency = from,
            ToCurrency = to,
            SourceAmount = sourceAmount,
            ExchangeRate = exchangeRate,
            TargetAmount = targetAmount,
            CashBoxWithdrawalId = withdrawal.Id,
            CashBoxDepositId = deposit.Id,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = userId,
            CreatedAt = now
        };
        _context.CashBoxCurrencyExchanges.Add(exchange);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return CashBoxExchangeResult.Ok(exchange.Id);
    }
}
