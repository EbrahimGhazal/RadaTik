using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class EmployeeWalletTopUpOutcome
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int? RequestId { get; init; }

    public static EmployeeWalletTopUpOutcome Ok(string message, int? requestId = null) => new()
    {
        Success = true,
        Message = message,
        RequestId = requestId
    };

    public static EmployeeWalletTopUpOutcome Fail(string message) => new()
    {
        Success = false,
        Message = message
    };
}

public sealed class EmployeeWalletTopUpService(
    ApplicationDbContext context,
    EmployeeWalletFundingService fundingService,
    EmployeeWalletTopUpCommissionService commissionService)
{
    public async Task<EmployeeWalletTransferPricingResult> PreviewTransferAsync(
        decimal amount,
        CancellationToken cancellationToken = default) =>
        await commissionService.CalculateAsync(amount, cancellationToken);

    public async Task<EmployeeWalletTopUpOutcome> SubmitRequestAsync(
        PayrollEmployee employee,
        int companyNetworkId,
        string requestedByUserId,
        decimal amount,
        EmployeeWalletTopUpRequestSource source,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        if (amount <= 0m)
        {
            return EmployeeWalletTopUpOutcome.Fail("المبلغ يجب أن يكون أكبر من صفر.");
        }

        if (!employee.IsActive)
        {
            return EmployeeWalletTopUpOutcome.Fail("لا يمكن تقديم طلب لحساب غير نشط.");
        }

        bool hasPending = await context.EmployeeWalletTopUpRequests.AnyAsync(
            r => r.PayrollEmployeeId == employee.Id
                 && r.Status == EmployeeWalletTopUpRequestStatus.Pending,
            cancellationToken);
        if (hasPending)
        {
            return EmployeeWalletTopUpOutcome.Fail("لديك طلب تغذية قيد الانتظار. انتظر الرد أو ألغِه أولاً.");
        }

        EmployeeWalletTransferPricingResult pricing = await commissionService.CalculateAsync(amount, cancellationToken);
        Network? company = await context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId, cancellationToken);
        if (company != null && company.Balance < pricing.TotalCompanyDebit)
        {
            return EmployeeWalletTopUpOutcome.Fail(
                $"رصيد محفظة الشركة غير كافٍ لتنفيذ الطلب عند الموافقة ({pricing.TotalCompanyDebit:N0} ل.س.ج مطلوب).");
        }

        EmployeeWalletTopUpRequest request = new()
        {
            CompanyNetworkId = companyNetworkId,
            PayrollEmployeeId = employee.Id,
            Amount = amount,
            PlatformCommissionAmount = pricing.CommissionSyp,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            RequestSource = source,
            RequestedByUserId = requestedByUserId,
            Status = EmployeeWalletTopUpRequestStatus.Pending
        };

        context.EmployeeWalletTopUpRequests.Add(request);
        await context.SaveChangesAsync(cancellationToken);

        return EmployeeWalletTopUpOutcome.Ok(
            "تم إرسال طلب تغذية المحفظة. سيتم مراجعته من مدير الشركة.",
            request.Id);
    }

    public async Task<EmployeeWalletTopUpOutcome> CancelPendingAsync(
        int requestId,
        int payrollEmployeeId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        EmployeeWalletTopUpRequest? request = await context.EmployeeWalletTopUpRequests
            .FirstOrDefaultAsync(
                r => r.Id == requestId
                     && r.PayrollEmployeeId == payrollEmployeeId
                     && r.RequestedByUserId == userId,
                cancellationToken);

        if (request == null)
        {
            return EmployeeWalletTopUpOutcome.Fail("الطلب غير موجود.");
        }

        if (request.Status != EmployeeWalletTopUpRequestStatus.Pending)
        {
            return EmployeeWalletTopUpOutcome.Fail("لا يمكن إلغاء طلب لم يعد قيد الانتظار.");
        }

        request.Status = EmployeeWalletTopUpRequestStatus.Cancelled;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = userId;
        await context.SaveChangesAsync(cancellationToken);

        return EmployeeWalletTopUpOutcome.Ok("تم إلغاء طلب التغذية.");
    }

    public async Task<EmployeeWalletTopUpOutcome> ApproveRequestAsync(
        int requestId,
        int companyNetworkId,
        string processorUserId,
        string? adminNotes,
        CancellationToken cancellationToken = default)
    {
        EmployeeWalletTopUpRequest? request = await context.EmployeeWalletTopUpRequests
            .Include(r => r.PayrollEmployee)
            .FirstOrDefaultAsync(
                r => r.Id == requestId && r.CompanyNetworkId == companyNetworkId,
                cancellationToken);

        if (request?.PayrollEmployee == null)
        {
            return EmployeeWalletTopUpOutcome.Fail("الطلب غير موجود.");
        }

        if (request.Status != EmployeeWalletTopUpRequestStatus.Pending)
        {
            return EmployeeWalletTopUpOutcome.Fail("تمت معالجة هذا الطلب مسبقاً.");
        }

        await using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction dbTx =
            await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            EmployeeWalletFundingResult funding = await fundingService.ExecuteFundingAsync(
                request.PayrollEmployee,
                companyNetworkId,
                request.Amount,
                processorUserId,
                EmployeeWalletTransactionSource.TopUpRequestApproved,
                request.Id,
                BuildFundingNote(request, adminNotes),
                cancellationToken);

            if (!funding.Success)
            {
                await dbTx.RollbackAsync(cancellationToken);
                return EmployeeWalletTopUpOutcome.Fail(funding.ErrorMessage ?? "تعذر تنفيذ التغذية.");
            }

            request.Status = EmployeeWalletTopUpRequestStatus.Approved;
            request.ProcessedByUserId = processorUserId;
            request.ProcessedAt = DateTime.UtcNow;
            request.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? null : adminNotes.Trim();
            request.PlatformCommissionAmount = funding.CommissionCharged;
            await context.SaveChangesAsync(cancellationToken);
            await dbTx.CommitAsync(cancellationToken);

            return EmployeeWalletTopUpOutcome.Ok(
                $"تمت الموافقة وتغذية محفظة {request.PayrollEmployee.FullName} بمبلغ {request.Amount:N0} ل.س.ج.");
        }
        catch
        {
            await dbTx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<EmployeeWalletTopUpOutcome> RejectRequestAsync(
        int requestId,
        int companyNetworkId,
        string processorUserId,
        string? adminNotes,
        CancellationToken cancellationToken = default)
    {
        EmployeeWalletTopUpRequest? request = await context.EmployeeWalletTopUpRequests
            .FirstOrDefaultAsync(
                r => r.Id == requestId && r.CompanyNetworkId == companyNetworkId,
                cancellationToken);

        if (request == null)
        {
            return EmployeeWalletTopUpOutcome.Fail("الطلب غير موجود.");
        }

        if (request.Status != EmployeeWalletTopUpRequestStatus.Pending)
        {
            return EmployeeWalletTopUpOutcome.Fail("تمت معالجة هذا الطلب مسبقاً.");
        }

        request.Status = EmployeeWalletTopUpRequestStatus.Rejected;
        request.ProcessedByUserId = processorUserId;
        request.ProcessedAt = DateTime.UtcNow;
        request.AdminNotes = string.IsNullOrWhiteSpace(adminNotes) ? null : adminNotes.Trim();
        await context.SaveChangesAsync(cancellationToken);

        return EmployeeWalletTopUpOutcome.Ok("تم رفض طلب التغذية.");
    }

    public async Task<EmployeeWalletTopUpOutcome> DirectTopUpAsync(
        PayrollEmployee employee,
        int companyNetworkId,
        string actorUserId,
        decimal amount,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        EmployeeWalletFundingResult funding = await fundingService.FundAsync(
            employee,
            companyNetworkId,
            amount,
            actorUserId,
            EmployeeWalletTransactionSource.DirectTopUpByManager,
            null,
            notes,
            cancellationToken);

        if (!funding.Success)
        {
            return EmployeeWalletTopUpOutcome.Fail(funding.ErrorMessage ?? "تعذر تنفيذ التغذية.");
        }

        return EmployeeWalletTopUpOutcome.Ok(
            $"تم تغذية محفظة {employee.FullName} بمبلغ {amount:N0} ل.س.ج. الرصيد الحالي: {funding.NewEmployeeBalance:N0} ل.س.ج.");
    }

    private static string? BuildFundingNote(EmployeeWalletTopUpRequest request, string? adminNotes)
    {
        string baseNote = $"طلب تغذية #{request.Id}";
        if (string.IsNullOrWhiteSpace(request.Notes))
        {
            return string.IsNullOrWhiteSpace(adminNotes) ? baseNote : $"{baseNote} — {adminNotes.Trim()}";
        }

        return string.IsNullOrWhiteSpace(adminNotes)
            ? $"{baseNote} — {request.Notes.Trim()}"
            : $"{baseNote} — {request.Notes.Trim()} — {adminNotes.Trim()}";
    }
}
