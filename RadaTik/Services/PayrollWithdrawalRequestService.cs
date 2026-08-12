using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class PayrollWithdrawalRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly CompanyPayrollService _payrollService;

    public PayrollWithdrawalRequestService(ApplicationDbContext context, CompanyPayrollService payrollService)
    {
        _context = context;
        _payrollService = payrollService;
    }

    public async Task<decimal> GetAvailableWithdrawalAmountAsync(
        PayrollEmployee employee,
        int year,
        int month,
        CancellationToken cancellationToken = default)
    {
        PayrollPayment? payment = await _context.PayrollPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.PayrollEmployeeId == employee.Id && p.Year == year && p.Month == month,
                cancellationToken);

        if (payment?.IsPaid == true)
        {
            return 0m;
        }

        PayrollMonthLedger ledger = await _payrollService.BuildMonthLedgerAsync(employee, year, month, payment, cancellationToken);

        decimal pendingRequests = await _context.PayrollWithdrawalRequests
            .AsNoTracking()
            .Where(r =>
                r.PayrollEmployeeId == employee.Id
                && r.Year == year
                && r.Month == month
                && r.Status == PayrollWithdrawalRequestStatus.Pending)
            .SumAsync(r => r.Amount, cancellationToken);

        return Math.Max(0m, ledger.NetPayable - pendingRequests);
    }

    public async Task<(bool Success, string Message)> SubmitAsync(
        PayrollEmployee employee,
        int companyNetworkId,
        string requestedByUserId,
        int year,
        int month,
        decimal amount,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (!employee.IsActive)
        {
            return (false, "لا يمكن تقديم طلب سحب لحساب غير نشط.");
        }

        if (!CompanyPayrollService.WasEmployedInMonth(employee, year, month))
        {
            return (false, "لا يوجد مستحق راتب في الشهر المحدد.");
        }

        if (amount <= 0m)
        {
            return (false, "المبلغ يجب أن يكون أكبر من صفر.");
        }

        decimal available = await GetAvailableWithdrawalAmountAsync(employee, year, month, cancellationToken);
        if (amount > available)
        {
            return (false, $"المبلغ يتجاوز المتاح للسحب ({available:N0} ل.س).");
        }

        bool duplicatePending = await _context.PayrollWithdrawalRequests.AnyAsync(
            r => r.PayrollEmployeeId == employee.Id
                 && r.Year == year
                 && r.Month == month
                 && r.Status == PayrollWithdrawalRequestStatus.Pending,
            cancellationToken);
        if (duplicatePending)
        {
            return (false, "لديك طلب سحب قيد الانتظار لهذا الشهر. انتظر الرد أو ألغِه أولاً.");
        }

        _context.PayrollWithdrawalRequests.Add(new PayrollWithdrawalRequest
        {
            CompanyNetworkId = companyNetworkId,
            PayrollEmployeeId = employee.Id,
            Year = year,
            Month = month,
            Amount = amount,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RequestedByUserId = requestedByUserId,
            Status = PayrollWithdrawalRequestStatus.Pending
        });
        await _context.SaveChangesAsync(cancellationToken);
        return (true, "تم إرسال طلب السحب. سيتم مراجعته من قسم الرواتب.");
    }

    public async Task<(bool Success, string Message)> CancelPendingAsync(
        int requestId,
        int payrollEmployeeId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        PayrollWithdrawalRequest? request = await _context.PayrollWithdrawalRequests
            .FirstOrDefaultAsync(
                r => r.Id == requestId
                     && r.PayrollEmployeeId == payrollEmployeeId
                     && r.RequestedByUserId == userId,
                cancellationToken);

        if (request == null)
        {
            return (false, "الطلب غير موجود.");
        }

        if (request.Status != PayrollWithdrawalRequestStatus.Pending)
        {
            return (false, "لا يمكن إلغاء طلب لم يعد قيد الانتظار.");
        }

        request.Status = PayrollWithdrawalRequestStatus.Cancelled;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewedByUserId = userId;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, "تم إلغاء طلب السحب.");
    }

    public async Task<(bool Success, string Message)> ReviewAsync(
        int requestId,
        int companyNetworkId,
        string reviewerUserId,
        bool approve,
        string? reviewNotes,
        CancellationToken cancellationToken = default)
    {
        PayrollWithdrawalRequest? request = await _context.PayrollWithdrawalRequests
            .Include(r => r.PayrollEmployee)
            .FirstOrDefaultAsync(
                r => r.Id == requestId && r.CompanyNetworkId == companyNetworkId,
                cancellationToken);

        if (request?.PayrollEmployee == null)
        {
            return (false, "الطلب غير موجود.");
        }

        if (request.Status != PayrollWithdrawalRequestStatus.Pending)
        {
            return (false, "تمت معالجة هذا الطلب مسبقاً.");
        }

        if (!approve)
        {
            request.Status = PayrollWithdrawalRequestStatus.Rejected;
            request.ReviewedByUserId = reviewerUserId;
            request.ReviewedAt = DateTime.UtcNow;
            request.ReviewNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();
            await _context.SaveChangesAsync(cancellationToken);
            return (true, "تم رفض طلب السحب.");
        }

        decimal available = await GetAvailableWithdrawalAmountAsync(
            request.PayrollEmployee,
            request.Year,
            request.Month,
            cancellationToken);
        if (request.Amount > available)
        {
            return (false, $"لا يمكن قبول الطلب — المتاح للسحب {available:N0} ل.س فقط.");
        }

        PayrollTransaction tx = new()
        {
            CompanyNetworkId = companyNetworkId,
            PayrollEmployeeId = request.PayrollEmployeeId,
            Type = PayrollTransactionType.MidMonthWithdrawal,
            Amount = request.Amount,
            Year = request.Year,
            Month = request.Month,
            TransactionDate = DateTime.UtcNow,
            Notes = $"طلب سحب #{request.Id}" + (string.IsNullOrWhiteSpace(request.Notes) ? "" : $" — {request.Notes}"),
            CreatedByUserId = reviewerUserId
        };
        _context.PayrollTransactions.Add(tx);
        await _context.SaveChangesAsync(cancellationToken);

        request.Status = PayrollWithdrawalRequestStatus.Approved;
        request.ReviewedByUserId = reviewerUserId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewNotes = string.IsNullOrWhiteSpace(reviewNotes) ? null : reviewNotes.Trim();
        request.PayrollTransactionId = tx.Id;
        await _context.SaveChangesAsync(cancellationToken);
        return (true, "تم قبول طلب السحب وتسجيل السحب على الراتب.");
    }
}
