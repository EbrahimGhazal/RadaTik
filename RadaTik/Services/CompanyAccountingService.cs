using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class CompanyAccountingService
{
    private readonly ApplicationDbContext _context;

    public CompanyAccountingService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task EnsureDefaultChartOfAccountsAsync(int companyNetworkId, CancellationToken ct = default)
    {
        bool exists = await _context.ChartOfAccounts
            .AnyAsync(a => a.CompanyNetworkId == companyNetworkId, ct);
        if (exists)
        {
            return;
        }

        List<ChartOfAccount> defaults =
        [
            new() { CompanyNetworkId = companyNetworkId, Code = "1000", Name = "الصندوق", AccountType = ChartOfAccountType.Asset },
            new() { CompanyNetworkId = companyNetworkId, Code = "1100", Name = "ذمم مدينة", AccountType = ChartOfAccountType.Asset },
            new() { CompanyNetworkId = companyNetworkId, Code = "1200", Name = "المخزون", AccountType = ChartOfAccountType.Asset },
            new() { CompanyNetworkId = companyNetworkId, Code = "2000", Name = "ذمم دائنة", AccountType = ChartOfAccountType.Liability },
            new() { CompanyNetworkId = companyNetworkId, Code = "3000", Name = "رأس المال", AccountType = ChartOfAccountType.Equity },
            new() { CompanyNetworkId = companyNetworkId, Code = "4000", Name = "إيرادات المبيعات", AccountType = ChartOfAccountType.Revenue },
            new() { CompanyNetworkId = companyNetworkId, Code = "4100", Name = "إيرادات الخدمات", AccountType = ChartOfAccountType.Revenue },
            new() { CompanyNetworkId = companyNetworkId, Code = "5000", Name = "مصروفات التشغيل", AccountType = ChartOfAccountType.Expense },
            new() { CompanyNetworkId = companyNetworkId, Code = "5100", Name = "رواتب وأجور", AccountType = ChartOfAccountType.Expense },
            new() { CompanyNetworkId = companyNetworkId, Code = "5200", Name = "مصروفات المشتريات", AccountType = ChartOfAccountType.Expense },
        ];

        _context.ChartOfAccounts.AddRange(defaults);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<(bool Success, string Message)> PostJournalEntryAsync(
        int entryId,
        int companyNetworkId,
        string postedByUserId,
        CancellationToken ct = default)
    {
        JournalEntry? entry = await _context.JournalEntries
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == entryId && e.CompanyNetworkId == companyNetworkId, ct);

        if (entry == null)
        {
            return (false, "القيد غير موجود.");
        }

        if (entry.Status != JournalEntryStatus.Draft)
        {
            return (false, "لا يمكن ترحيل هذا القيد.");
        }

        if (entry.Lines.Count < 2)
        {
            return (false, "يجب أن يحتوي القيد على سطرين على الأقل.");
        }

        decimal totalDebit = entry.Lines.Sum(l => l.Debit);
        decimal totalCredit = entry.Lines.Sum(l => l.Credit);
        if (totalDebit != totalCredit || totalDebit <= 0)
        {
            return (false, "يجب أن يتساوى مجموع المدين مع مجموع الدائن.");
        }

        entry.Status = JournalEntryStatus.Posted;
        entry.PostedByUserId = postedByUserId;
        entry.PostedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(ct);

        return (true, "تم ترحيل القيد بنجاح.");
    }
}
