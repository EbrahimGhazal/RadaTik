using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class ErpSummary
{
    public int ActiveEmployees { get; init; }
    public int ActiveErpCustomers { get; init; }
    public int ActiveErpSuppliers { get; init; }
    public int OpenTasks { get; init; }
    public int OverdueTasks { get; init; }
    public int PendingRewardPenalties { get; init; }
    public int ActivePayrollEmployees { get; init; }
    public int ActiveWarehouseItems { get; init; }
    public int UnpaidSalesInvoices { get; init; }
    public decimal UnpaidSalesTotal { get; init; }
    public int PostedJournalEntriesThisMonth { get; init; }
    public int ChartOfAccountsCount { get; init; }
}

public interface IErpSummaryService
{
    Task<ErpSummary> GetSummaryAsync(int companyNetworkId, CancellationToken ct = default);
}

public sealed class ErpSummaryService : IErpSummaryService
{
    private readonly ApplicationDbContext _context;

    public ErpSummaryService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ErpSummary> GetSummaryAsync(int companyNetworkId, CancellationToken ct = default)
    {
        List<int> networkIds = await _context.Networks.AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync(ct);

        DateTime now = DateTime.UtcNow;
        DateTime monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        int activeEmployees = await _context.Users.AsNoTracking()
            .CountAsync(u =>
                u.NetworkId != null
                && networkIds.Contains(u.NetworkId.Value)
                && u.IsActive, ct);

        int openTasks = await _context.CompanyEmployeeTasks.AsNoTracking()
            .CountAsync(t =>
                t.CompanyNetworkId == companyNetworkId
                && (t.Status == CompanyEmployeeTaskStatus.Pending
                    || t.Status == CompanyEmployeeTaskStatus.InProgress), ct);

        int overdueTasks = await _context.CompanyEmployeeTasks.AsNoTracking()
            .CountAsync(t =>
                t.CompanyNetworkId == companyNetworkId
                && t.DueDate != null
                && t.DueDate < now
                && t.Status != CompanyEmployeeTaskStatus.Completed
                && t.Status != CompanyEmployeeTaskStatus.Cancelled, ct);

        return new ErpSummary
        {
            ActiveEmployees = activeEmployees,
            ActiveErpCustomers = await _context.ErpCustomers.AsNoTracking()
                .CountAsync(c => c.CompanyNetworkId == companyNetworkId && c.IsActive, ct),
            ActiveErpSuppliers = await _context.ErpSuppliers.AsNoTracking()
                .CountAsync(s => s.CompanyNetworkId == companyNetworkId && s.IsActive, ct),
            OpenTasks = openTasks,
            OverdueTasks = overdueTasks,
            PendingRewardPenalties = await _context.EmployeeRewardPenalties.AsNoTracking()
                .CountAsync(r =>
                    r.CompanyNetworkId == companyNetworkId
                    && r.Status == EmployeeRewardPenaltyStatus.Pending, ct),
            ActivePayrollEmployees = await _context.PayrollEmployees.AsNoTracking()
                .CountAsync(e => e.CompanyNetworkId == companyNetworkId && e.IsActive, ct),
            ActiveWarehouseItems = await _context.WarehouseItems.AsNoTracking()
                .CountAsync(w => w.CompanyNetworkId == companyNetworkId && w.IsActive, ct),
            UnpaidSalesInvoices = await _context.MaterialSalesInvoices.AsNoTracking()
                .CountAsync(i =>
                    i.CompanyNetworkId == companyNetworkId
                    && !i.IsPaid
                    && !i.IsCancelled, ct),
            UnpaidSalesTotal = await _context.MaterialSalesInvoices.AsNoTracking()
                .Where(i =>
                    i.CompanyNetworkId == companyNetworkId
                    && !i.IsPaid
                    && !i.IsCancelled)
                .SumAsync(i => i.TotalAmount, ct),
            PostedJournalEntriesThisMonth = await _context.JournalEntries.AsNoTracking()
                .CountAsync(j =>
                    j.CompanyNetworkId == companyNetworkId
                    && j.Status == JournalEntryStatus.Posted
                    && j.PostedAt >= monthStart, ct),
            ChartOfAccountsCount = await _context.ChartOfAccounts.AsNoTracking()
                .CountAsync(a => a.CompanyNetworkId == companyNetworkId && a.IsActive, ct),
        };
    }
}
