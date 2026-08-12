using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class CompanyWalletCashTransferResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int? WalletTransactionId { get; init; }
    public int? CashBoxWithdrawalId { get; init; }

    public static CompanyWalletCashTransferResult Ok(int? walletTxId = null, int? cashWithdrawalId = null) =>
        new() { Success = true, WalletTransactionId = walletTxId, CashBoxWithdrawalId = cashWithdrawalId };

    public static CompanyWalletCashTransferResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}

/// <summary>خصم صندوق الشركة عند موافقة مدير النظام على طلب تغذية المحفظة.</summary>
public sealed class CompanyWalletCashTransferService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    /// <summary>
    /// يخصم من صندوق الشركة النقدي فقط (يُستدعى قبل إضافة المبلغ للمحفظة عند الموافقة على الطلب).
    /// </summary>
    public async Task<CompanyWalletCashTransferResult> WithdrawCompanyCashBoxForTopUpApprovalAsync(
        int companyNetworkId,
        int topUpRequestId,
        decimal amountSyp,
        string userId,
        CancellationToken ct = default)
    {
        if (amountSyp < 0.01m)
        {
            return CompanyWalletCashTransferResult.Fail("المبلغ غير صالح.");
        }

        bool alreadyWithdrawn = await _context.CashBoxWithdrawals
            .AnyAsync(w => w.NetworkTopUpRequestId == topUpRequestId, ct);
        if (alreadyWithdrawn)
        {
            return CompanyWalletCashTransferResult.Fail("تم خصم الصندوق مسبقاً لهذا الطلب.");
        }

        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(
            _context, CashBoxOwnerType.Network, companyNetworkId);
        if (cashBox == null)
        {
            return CompanyWalletCashTransferResult.Fail("تعذر الوصول للصندوق النقدي.");
        }

        if (!CashBoxHelper.HasSufficientBalance(cashBox, PricingCurrency.SYP_New, amountSyp))
        {
            return CompanyWalletCashTransferResult.Fail(
                "رصيد الصندوق غير كافٍ للموافقة. " +
                CashBoxHelper.FormatInsufficientBalanceMessage(cashBox, PricingCurrency.SYP_New, amountSyp));
        }

        decimal cashBefore = CashBoxHelper.GetBalance(cashBox, PricingCurrency.SYP_New);
        CashBoxHelper.ApplyDelta(cashBox, PricingCurrency.SYP_New, -amountSyp);

        CashBoxWithdrawal withdrawal = new()
        {
            CashBoxId = cashBox.Id,
            Amount = amountSyp,
            Currency = PricingCurrency.SYP_New,
            WithdrawnAt = DateTime.Now,
            WithdrawnByUserId = userId,
            NetworkTopUpRequestId = topUpRequestId,
            Notes = $"خصم صندوق عند موافقة طلب تغذية محفظة #{topUpRequestId}",
            BalanceBefore = cashBefore,
            BalanceAfter = CashBoxHelper.GetBalance(cashBox, PricingCurrency.SYP_New)
        };
        _context.CashBoxWithdrawals.Add(withdrawal);
        await _context.SaveChangesAsync(ct);

        return CompanyWalletCashTransferResult.Ok(cashWithdrawalId: withdrawal.Id);
    }
}
