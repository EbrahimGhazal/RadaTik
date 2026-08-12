using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class MaterialInvoiceAccountingResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public static MaterialInvoiceAccountingResult Ok() => new() { Success = true };
    public static MaterialInvoiceAccountingResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

/// <summary>
/// عند الدفع النقدي (بدون المحفظة): دفتر الإيراد/المصروف + الصندوق.
/// عند الدفع من المحفظة: المحفظة فقط — لا دفتر ولا صندوق (منفصلان عن المحفظة).
/// </summary>
public sealed class MaterialInvoiceAccountingService(ApplicationDbContext context)
{
    private readonly ApplicationDbContext _context = context;

    public async Task<MaterialInvoiceAccountingResult> SyncPurchasePaymentAsync(
      MaterialPurchaseInvoice invoice,
      string userId,
      bool isPaid,
      bool linkWallet,
      CancellationToken ct = default)
    {
        if (!isPaid)
        {
            await ClearPurchasePaymentAsync(invoice, userId, ct);
            return MaterialInvoiceAccountingResult.Ok();
        }

        if (!linkWallet && !invoice.MoneyDiaryEntryId.HasValue)
        {
            MaterialInvoiceAccountingResult diary = await CreatePurchaseDiaryAsync(invoice, userId, ct);
            if (!diary.Success)
            {
                return diary;
            }
        }

        if (linkWallet && invoice.MoneyDiaryEntryId.HasValue)
        {
            await ClearPurchaseDiaryOnlyAsync(invoice, ct);
        }

        if (!linkWallet && !invoice.CashBoxWithdrawalId.HasValue)
        {
            MaterialInvoiceAccountingResult cash = await CreatePurchaseCashWithdrawalAsync(invoice, userId, ct);
            if (!cash.Success)
            {
                return cash;
            }
        }

        if (linkWallet && invoice.CashBoxWithdrawalId.HasValue)
        {
            await ClearPurchaseCashAsync(invoice, userId, ct);
        }

        return MaterialInvoiceAccountingResult.Ok();
    }

    public async Task<MaterialInvoiceAccountingResult> SyncSalesPaymentAsync(
      MaterialSalesInvoice invoice,
      string userId,
      bool isPaid,
      bool linkWallet,
      CancellationToken ct = default)
    {
        if (!isPaid)
        {
            await ClearSalesPaymentAsync(invoice, userId, ct);
            return MaterialInvoiceAccountingResult.Ok();
        }

        if (!linkWallet && !invoice.MoneyDiaryEntryId.HasValue)
        {
            MaterialInvoiceAccountingResult diary = await CreateSalesDiaryAsync(invoice, userId, ct);
            if (!diary.Success)
            {
                return diary;
            }
        }

        if (linkWallet && invoice.MoneyDiaryEntryId.HasValue)
        {
            await ClearSalesDiaryOnlyAsync(invoice, ct);
        }

        if (!linkWallet && !invoice.CashBoxDepositId.HasValue)
        {
            MaterialInvoiceAccountingResult cash = await CreateSalesCashDepositAsync(invoice, userId, ct);
            if (!cash.Success)
            {
                return cash;
            }
        }

        if (linkWallet && invoice.CashBoxDepositId.HasValue)
        {
            await ClearSalesCashAsync(invoice, userId, ct);
        }

        return MaterialInvoiceAccountingResult.Ok();
    }

    public async Task<MaterialInvoiceAccountingResult> ReversePurchasePaymentAsync(
      MaterialPurchaseInvoice invoice,
      string userId,
      CancellationToken ct = default)
    {
        await ClearPurchasePaymentAsync(invoice, userId, ct);
        return MaterialInvoiceAccountingResult.Ok();
    }

    public async Task<MaterialInvoiceAccountingResult> ReverseSalesPaymentAsync(
      MaterialSalesInvoice invoice,
      string userId,
      CancellationToken ct = default)
    {
        try
        {
            await ClearSalesPaymentAsync(invoice, userId, ct);
            return MaterialInvoiceAccountingResult.Ok();
        }
        catch (InvalidOperationException ex)
        {
            return MaterialInvoiceAccountingResult.Fail(ex.Message);
        }
    }

    private async Task<MaterialInvoiceAccountingResult> CreatePurchaseDiaryAsync(
      MaterialPurchaseInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        MoneyDiaryEntry entry = new()
        {
            CompanyNetworkId = invoice.CompanyNetworkId,
            EntryType = MoneyDiaryEntryType.Expense,
            CategoryKey = "expense_purchase",
            Amount = invoice.TotalAmount,
            Currency = CashBoxHelper.NormalizeOperatingCurrency(invoice.Currency),
            EntryDate = invoice.InvoiceDate.Date,
            Description = $"فاتورة شراء مواد #{invoice.Id}" + (string.IsNullOrWhiteSpace(invoice.SupplierName) ? "" : $" — {invoice.SupplierName}"),
            CreatedByUserId = userId,
            MaterialPurchaseInvoiceId = invoice.Id
        };
        _context.MoneyDiaryEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        invoice.MoneyDiaryEntryId = entry.Id;
        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceAccountingResult.Ok();
    }

    private async Task<MaterialInvoiceAccountingResult> CreateSalesDiaryAsync(
      MaterialSalesInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        MoneyDiaryEntry entry = new()
        {
            CompanyNetworkId = invoice.CompanyNetworkId,
            EntryType = MoneyDiaryEntryType.Income,
            CategoryKey = "income_equipment_sale",
            Amount = invoice.TotalAmount,
            Currency = CashBoxHelper.NormalizeOperatingCurrency(invoice.Currency),
            EntryDate = invoice.InvoiceDate.Date,
            Description = $"فاتورة بيع مواد #{invoice.Id}" + (string.IsNullOrWhiteSpace(invoice.CustomerName) ? "" : $" — {invoice.CustomerName}"),
            CreatedByUserId = userId,
            MaterialSalesInvoiceId = invoice.Id
        };
        _context.MoneyDiaryEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
        invoice.MoneyDiaryEntryId = entry.Id;
        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceAccountingResult.Ok();
    }

    private async Task<MaterialInvoiceAccountingResult> CreatePurchaseCashWithdrawalAsync(
      MaterialPurchaseInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        CashBox? box = await CashBoxHelper.GetOrCreateCashBoxAsync(
          _context, CashBoxOwnerType.Network, invoice.CompanyNetworkId);
        if (box == null)
        {
            return MaterialInvoiceAccountingResult.Fail("تعذر الوصول للصندوق النقدي.");
        }

        PricingCurrency currency = CashBoxHelper.NormalizeOperatingCurrency(invoice.Currency);
        if (!CashBoxHelper.HasSufficientBalance(box, currency, invoice.TotalAmount))
        {
            return MaterialInvoiceAccountingResult.Fail(
              CashBoxHelper.FormatInsufficientBalanceMessage(box, currency, invoice.TotalAmount) +
              $" (فاتورة شراء #{invoice.Id}).");
        }

        decimal before = CashBoxHelper.GetBalance(box, currency);
        CashBoxHelper.ApplyDelta(box, currency, -invoice.TotalAmount);

        CashBoxWithdrawal w = new()
        {
            CashBoxId = box.Id,
            Amount = invoice.TotalAmount,
            Currency = currency,
            WithdrawnAt = invoice.PaidAt ?? invoice.InvoiceDate,
            WithdrawnByUserId = userId,
            Notes = $"دفع فاتورة شراء مواد #{invoice.Id}",
            BalanceBefore = before,
            BalanceAfter = CashBoxHelper.GetBalance(box, currency),
            MaterialPurchaseInvoiceId = invoice.Id
        };
        _context.CashBoxWithdrawals.Add(w);
        await _context.SaveChangesAsync(ct);
        invoice.CashBoxWithdrawalId = w.Id;
        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceAccountingResult.Ok();
    }

    private async Task<MaterialInvoiceAccountingResult> CreateSalesCashDepositAsync(
      MaterialSalesInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        CashBox? box = await CashBoxHelper.GetOrCreateCashBoxAsync(
          _context, CashBoxOwnerType.Network, invoice.CompanyNetworkId);
        if (box == null)
        {
            return MaterialInvoiceAccountingResult.Fail("تعذر الوصول للصندوق النقدي.");
        }

        PricingCurrency currency = CashBoxHelper.NormalizeOperatingCurrency(invoice.Currency);
        decimal before = CashBoxHelper.GetBalance(box, currency);
        CashBoxHelper.ApplyDelta(box, currency, invoice.TotalAmount);

        CashBoxDeposit d = new()
        {
            CashBoxId = box.Id,
            Amount = invoice.TotalAmount,
            Currency = currency,
            DepositedAt = invoice.PaidAt ?? invoice.InvoiceDate,
            DepositedByUserId = userId,
            Notes = $"تحصيل فاتورة بيع مواد #{invoice.Id}",
            BalanceBefore = before,
            BalanceAfter = CashBoxHelper.GetBalance(box, currency),
            MaterialSalesInvoiceId = invoice.Id
        };
        _context.CashBoxDeposits.Add(d);
        await _context.SaveChangesAsync(ct);
        invoice.CashBoxDepositId = d.Id;
        await _context.SaveChangesAsync(ct);
        return MaterialInvoiceAccountingResult.Ok();
    }

    private async Task ClearPurchaseDiaryOnlyAsync(MaterialPurchaseInvoice invoice, CancellationToken ct)
    {
        if (!invoice.MoneyDiaryEntryId.HasValue)
        {
            return;
        }

        MoneyDiaryEntry? entry = await _context.MoneyDiaryEntries
          .FirstOrDefaultAsync(e => e.Id == invoice.MoneyDiaryEntryId.Value, ct);
        if (entry != null)
        {
            _context.MoneyDiaryEntries.Remove(entry);
        }

        invoice.MoneyDiaryEntryId = null;
        await _context.SaveChangesAsync(ct);
    }

    private async Task ClearSalesDiaryOnlyAsync(MaterialSalesInvoice invoice, CancellationToken ct)
    {
        if (!invoice.MoneyDiaryEntryId.HasValue)
        {
            return;
        }

        MoneyDiaryEntry? entry = await _context.MoneyDiaryEntries
          .FirstOrDefaultAsync(e => e.Id == invoice.MoneyDiaryEntryId.Value, ct);
        if (entry != null)
        {
            _context.MoneyDiaryEntries.Remove(entry);
        }

        invoice.MoneyDiaryEntryId = null;
        await _context.SaveChangesAsync(ct);
    }

    private async Task ClearPurchasePaymentAsync(
      MaterialPurchaseInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        await ClearPurchaseCashAsync(invoice, userId, ct);
        if (invoice.MoneyDiaryEntryId is int diaryId)
        {
            MoneyDiaryEntry? entry = await _context.MoneyDiaryEntries
              .FirstOrDefaultAsync(e => e.Id == diaryId, ct);
            if (entry != null)
            {
                _context.MoneyDiaryEntries.Remove(entry);
            }

            invoice.MoneyDiaryEntryId = null;
        }
    }

    private async Task ClearSalesPaymentAsync(
      MaterialSalesInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        await ClearSalesCashAsync(invoice, userId, ct);
        if (invoice.MoneyDiaryEntryId is int diaryId)
        {
            MoneyDiaryEntry? entry = await _context.MoneyDiaryEntries
              .FirstOrDefaultAsync(e => e.Id == diaryId, ct);
            if (entry != null)
            {
                _context.MoneyDiaryEntries.Remove(entry);
            }

            invoice.MoneyDiaryEntryId = null;
        }
    }

    private async Task ClearPurchaseCashAsync(
      MaterialPurchaseInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        if (!invoice.CashBoxWithdrawalId.HasValue)
        {
            return;
        }

        CashBoxWithdrawal? withdrawal = await _context.CashBoxWithdrawals
          .Include(w => w.CashBox)
          .FirstOrDefaultAsync(w => w.Id == invoice.CashBoxWithdrawalId.Value, ct);
        if (withdrawal?.CashBox != null)
        {
            PricingCurrency currency = CashBoxHelper.NormalizeOperatingCurrency(withdrawal.Currency);
            decimal before = CashBoxHelper.GetBalance(withdrawal.CashBox, currency);
            CashBoxHelper.ApplyDelta(withdrawal.CashBox, currency, withdrawal.Amount);

            _context.CashBoxDeposits.Add(new CashBoxDeposit
            {
                CashBoxId = withdrawal.CashBoxId,
                Amount = withdrawal.Amount,
                Currency = currency,
                DepositedAt = DateTime.Now,
                DepositedByUserId = userId,
                Notes = $"عكس سحب — فاتورة شراء #{invoice.Id}",
                BalanceBefore = before,
                BalanceAfter = CashBoxHelper.GetBalance(withdrawal.CashBox, currency)
            });
        }

        invoice.CashBoxWithdrawalId = null;
        await _context.SaveChangesAsync(ct);
    }

    private async Task ClearSalesCashAsync(
      MaterialSalesInvoice invoice,
      string userId,
      CancellationToken ct)
    {
        if (!invoice.CashBoxDepositId.HasValue)
        {
            return;
        }

        CashBoxDeposit? deposit = await _context.CashBoxDeposits
          .Include(d => d.CashBox)
          .FirstOrDefaultAsync(d => d.Id == invoice.CashBoxDepositId.Value, ct);
        if (deposit?.CashBox != null)
        {
            PricingCurrency currency = CashBoxHelper.NormalizeOperatingCurrency(deposit.Currency);
            if (!CashBoxHelper.HasSufficientBalance(deposit.CashBox, currency, deposit.Amount))
            {
                throw new InvalidOperationException(
                  CashBoxHelper.FormatInsufficientBalanceMessage(deposit.CashBox, currency, deposit.Amount) +
                  " (عكس تحصيل فاتورة البيع).");
            }

            decimal before = CashBoxHelper.GetBalance(deposit.CashBox, currency);
            CashBoxHelper.ApplyDelta(deposit.CashBox, currency, -deposit.Amount);

            _context.CashBoxWithdrawals.Add(new CashBoxWithdrawal
            {
                CashBoxId = deposit.CashBoxId,
                Amount = deposit.Amount,
                Currency = currency,
                WithdrawnAt = DateTime.Now,
                WithdrawnByUserId = userId,
                Notes = $"عكس إيداع — فاتورة بيع #{invoice.Id}",
                BalanceBefore = before,
                BalanceAfter = CashBoxHelper.GetBalance(deposit.CashBox, currency)
            });
        }

        invoice.CashBoxDepositId = null;
        await _context.SaveChangesAsync(ct);
    }
}
