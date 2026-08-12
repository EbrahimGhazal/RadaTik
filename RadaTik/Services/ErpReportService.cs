using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class ErpSalesByCustomerRow
{
    public int? ErpCustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public int InvoiceCount { get; init; }
    public decimal TotalAmount { get; init; }
    public decimal UnpaidAmount { get; init; }
}

public sealed class ErpUnpaidSalesRow
{
    public int InvoiceId { get; init; }
    public DateTime InvoiceDate { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public PricingCurrency Currency { get; init; }
    public int DaysOutstanding { get; init; }
}

public sealed class ErpEmployeeTaskPerformanceRow
{
    public string UserId { get; init; } = string.Empty;
    public string EmployeeName { get; init; } = string.Empty;
    public int TotalTasks { get; init; }
    public int CompletedTasks { get; init; }
    public int OpenTasks { get; init; }
    public int OverdueTasks { get; init; }
    public int RewardsCount { get; init; }
    public int PenaltiesCount { get; init; }
}

public sealed class ErpReportsData
{
    public IReadOnlyList<ErpSalesByCustomerRow> SalesByCustomer { get; init; } = Array.Empty<ErpSalesByCustomerRow>();
    public IReadOnlyList<ErpUnpaidSalesRow> UnpaidSales { get; init; } = Array.Empty<ErpUnpaidSalesRow>();
    public IReadOnlyList<ErpEmployeeTaskPerformanceRow> EmployeePerformance { get; init; } = Array.Empty<ErpEmployeeTaskPerformanceRow>();
    public decimal UnpaidSalesGrandTotal { get; init; }
}

public interface IErpReportService
{
    Task<ErpReportsData> GetReportsAsync(
        int companyNetworkId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default);
}

public sealed class ErpReportService : IErpReportService
{
    private readonly ApplicationDbContext _context;

    public ErpReportService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ErpReportsData> GetReportsAsync(
        int companyNetworkId,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        DateTime from = fromDate?.Date ?? now.AddMonths(-3).Date;
        DateTime to = toDate?.Date.AddDays(1).AddTicks(-1) ?? now;

        IQueryable<MaterialSalesInvoice> salesQuery = _context.MaterialSalesInvoices.AsNoTracking()
            .Include(i => i.ErpCustomer)
            .Where(i =>
                i.CompanyNetworkId == companyNetworkId
                && !i.IsCancelled
                && i.InvoiceDate >= from
                && i.InvoiceDate <= to);

        List<MaterialSalesInvoice> salesInvoices = await salesQuery.ToListAsync(ct);

        List<ErpSalesByCustomerRow> salesByCustomer = salesInvoices
            .GroupBy(i => new { i.ErpCustomerId, Name = i.ErpCustomer?.Name ?? i.CustomerName ?? "—" })
            .Select(g => new ErpSalesByCustomerRow
            {
                ErpCustomerId = g.Key.ErpCustomerId,
                CustomerName = g.Key.Name,
                InvoiceCount = g.Count(),
                TotalAmount = g.Sum(x => x.TotalAmount),
                UnpaidAmount = g.Where(x => !x.IsPaid).Sum(x => x.TotalAmount),
            })
            .OrderByDescending(r => r.TotalAmount)
            .ToList();

        List<ErpUnpaidSalesRow> unpaidSales = await _context.MaterialSalesInvoices.AsNoTracking()
            .Where(i =>
                i.CompanyNetworkId == companyNetworkId
                && !i.IsPaid
                && !i.IsCancelled)
            .OrderBy(i => i.InvoiceDate)
            .Select(i => new ErpUnpaidSalesRow
            {
                InvoiceId = i.Id,
                InvoiceDate = i.InvoiceDate,
                CustomerName = i.CustomerName ?? "—",
                TotalAmount = i.TotalAmount,
                Currency = i.Currency,
                DaysOutstanding = (int)(now - i.InvoiceDate).TotalDays,
            })
            .ToListAsync(ct);

        List<CompanyEmployeeTask> tasks = await _context.CompanyEmployeeTasks.AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Where(t => t.CompanyNetworkId == companyNetworkId)
            .ToListAsync(ct);

        Dictionary<string, (int Rewards, int Penalties)> rewardStats = await _context.EmployeeRewardPenalties.AsNoTracking()
            .Include(r => r.PayrollEmployee)
            .Where(r =>
                r.CompanyNetworkId == companyNetworkId
                && r.Status == EmployeeRewardPenaltyStatus.AppliedToPayroll)
            .GroupBy(r => r.PayrollEmployee!.ApplicationUserId ?? string.Empty)
            .Select(g => new
            {
                UserId = g.Key,
                Rewards = g.Count(x => x.Type == EmployeeRewardPenaltyType.Reward),
                Penalties = g.Count(x => x.Type == EmployeeRewardPenaltyType.Penalty),
            })
            .ToDictionaryAsync(x => x.UserId, x => (x.Rewards, x.Penalties), ct);

        List<ErpEmployeeTaskPerformanceRow> employeePerformance = tasks
            .GroupBy(t => new { t.AssignedToUserId, Name = t.AssignedToUser?.FullName ?? t.AssignedToUser?.UserName ?? t.AssignedToUserId })
            .Select(g =>
            {
                rewardStats.TryGetValue(g.Key.AssignedToUserId, out (int Rewards, int Penalties) rp);
                return new ErpEmployeeTaskPerformanceRow
                {
                    UserId = g.Key.AssignedToUserId,
                    EmployeeName = g.Key.Name,
                    TotalTasks = g.Count(),
                    CompletedTasks = g.Count(t => t.Status == CompanyEmployeeTaskStatus.Completed),
                    OpenTasks = g.Count(t =>
                        t.Status == CompanyEmployeeTaskStatus.Pending
                        || t.Status == CompanyEmployeeTaskStatus.InProgress),
                    OverdueTasks = g.Count(t =>
                        t.DueDate != null
                        && t.DueDate < now
                        && t.Status != CompanyEmployeeTaskStatus.Completed
                        && t.Status != CompanyEmployeeTaskStatus.Cancelled),
                    RewardsCount = rp.Rewards,
                    PenaltiesCount = rp.Penalties,
                };
            })
            .OrderByDescending(r => r.OpenTasks)
            .ThenByDescending(r => r.TotalTasks)
            .ToList();

        return new ErpReportsData
        {
            SalesByCustomer = salesByCustomer,
            UnpaidSales = unpaidSales,
            EmployeePerformance = employeePerformance,
            UnpaidSalesGrandTotal = unpaidSales.Sum(r => r.TotalAmount),
        };
    }
}
