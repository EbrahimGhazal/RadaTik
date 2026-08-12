using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class EmployeeRewardPenaltyService
{
    private readonly ApplicationDbContext _context;

    public EmployeeRewardPenaltyService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<(bool Success, string Message)> ReviewAsync(
        int id,
        int companyNetworkId,
        string reviewerUserId,
        bool approve,
        string? reviewNotes,
        CancellationToken ct = default)
    {
        EmployeeRewardPenalty? record = await _context.EmployeeRewardPenalties
            .Include(r => r.PayrollEmployee)
            .FirstOrDefaultAsync(r => r.Id == id && r.CompanyNetworkId == companyNetworkId, ct);

        if (record == null)
        {
            return (false, "السجل غير موجود.");
        }

        if (record.Status != EmployeeRewardPenaltyStatus.Pending)
        {
            return (false, "تمت معالجة هذا السجل مسبقاً.");
        }

        record.ReviewedByUserId = reviewerUserId;
        record.ReviewedAt = DateTime.UtcNow;
        record.ReviewNotes = reviewNotes?.Trim();

        if (!approve)
        {
            record.Status = EmployeeRewardPenaltyStatus.Rejected;
            await _context.SaveChangesAsync(ct);
            return (true, "تم رفض السجل.");
        }

        record.Status = EmployeeRewardPenaltyStatus.Approved;

        DateTime now = DateTime.UtcNow;
        PayrollTransactionType txType = record.Type == EmployeeRewardPenaltyType.Reward
            ? PayrollTransactionType.Bonus
            : PayrollTransactionType.Deduction;

        PayrollTransaction tx = new PayrollTransaction
        {
            CompanyNetworkId = companyNetworkId,
            PayrollEmployeeId = record.PayrollEmployeeId,
            Year = now.Year,
            Month = now.Month,
            Type = txType,
            Amount = record.Amount,
            Notes = record.Reason,
            TransactionDate = now,
            CreatedAt = now,
        };

        _context.PayrollTransactions.Add(tx);
        await _context.SaveChangesAsync(ct);

        record.PayrollTransactionId = tx.Id;
        record.Status = EmployeeRewardPenaltyStatus.AppliedToPayroll;
        await _context.SaveChangesAsync(ct);

        return (true, record.Type == EmployeeRewardPenaltyType.Reward
            ? "تم اعتماد المكافأة وتطبيقها على الراتب."
            : "تم اعتماد العقوبة وتطبيقها على الراتب.");
    }
}
