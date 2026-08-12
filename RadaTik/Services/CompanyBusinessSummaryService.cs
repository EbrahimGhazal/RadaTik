using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

public sealed class CompanyBusinessSummary
{
    public int LowOrEmptyStockItems { get; init; }
    public int ActiveWarehouseItems { get; init; }
    public decimal MoneyDiaryIncomeThisMonth { get; init; }
    public decimal MoneyDiaryExpenseThisMonth { get; init; }
    public decimal MoneyDiaryNetThisMonth => MoneyDiaryIncomeThisMonth - MoneyDiaryExpenseThisMonth;

    public decimal MoneyDiaryIncomeUsdThisMonth { get; init; }
    public decimal MoneyDiaryExpenseUsdThisMonth { get; init; }
    public decimal MoneyDiaryNetUsdThisMonth => MoneyDiaryIncomeUsdThisMonth - MoneyDiaryExpenseUsdThisMonth;
    public int PayrollPendingCount { get; init; }
    public decimal PayrollPendingAmount { get; init; }
    public int ActivePayrollEmployees { get; init; }
    public int PendingMaintenanceInvoices { get; init; }
    public decimal PendingMaintenanceInvoicesAmount { get; init; }
    public decimal CashBoxBalance { get; init; }
    public decimal CashBoxBalanceUsd { get; init; }
    public decimal UnpaidMaterialPurchaseTotal { get; init; }
    public decimal UnpaidMaterialSalesTotal { get; init; }
}

public interface ICompanyBusinessSummaryService
{
    Task<CompanyBusinessSummary> GetSummaryAsync(int companyNetworkId, CancellationToken ct = default);
}

public sealed class CompanyBusinessSummaryService(
  ApplicationDbContext context,
  IWarehouseStockService warehouseStock) : ICompanyBusinessSummaryService
{
    private readonly ApplicationDbContext _context = context;
    private readonly IWarehouseStockService _warehouseStock = warehouseStock;

    public async Task<CompanyBusinessSummary> GetSummaryAsync(int companyNetworkId, CancellationToken ct = default)
    {
        DateTime monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        DateTime monthEnd = monthStart.AddMonths(1);

        List<WarehouseItem> items = await _context.WarehouseItems
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == companyNetworkId && i.IsActive)
          .ToListAsync(ct);

        Dictionary<int, decimal> onHand = await _warehouseStock.GetOnHandByItemIdAsync(companyNetworkId, ct);
        int lowStock = items.Count(i => onHand.GetValueOrDefault(i.Id, 0m) <= 0m);

        List<MoneyDiaryEntry> diary = await _context.MoneyDiaryEntries
          .AsNoTracking()
          .Where(e => e.CompanyNetworkId == companyNetworkId && e.EntryDate >= monthStart && e.EntryDate < monthEnd)
          .ToListAsync(ct);

        int year = DateTime.Today.Year;
        int month = DateTime.Today.Month;
        List<PayrollPayment> payroll = await _context.PayrollPayments
          .AsNoTracking()
          .Where(p => p.CompanyNetworkId == companyNetworkId && p.Year == year && p.Month == month && !p.IsPaid)
          .ToListAsync(ct);

        int employeeCount = await _context.PayrollEmployees
          .AsNoTracking()
          .CountAsync(e => e.CompanyNetworkId == companyNetworkId && e.IsActive, ct);

        List<int> scopeIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);
        List<MaintenanceInvoice> pendingMaintenance = await _context.MaintenanceInvoices
          .AsNoTracking()
          .Where(i => scopeIds.Contains(i.NetworkId) && i.Status == MaintenanceInvoiceStatus.Pending)
          .ToListAsync(ct);

        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(
          _context, CashBoxOwnerType.Network, companyNetworkId);
        List<MaterialPurchaseInvoice> materialPurchases = await _context.MaterialPurchaseInvoices
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == companyNetworkId && !i.IsCancelled && !i.IsPaid)
          .ToListAsync(ct);
        List<MaterialSalesInvoice> materialSales = await _context.MaterialSalesInvoices
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == companyNetworkId && !i.IsCancelled && !i.IsPaid)
          .ToListAsync(ct);

        return new CompanyBusinessSummary
        {
            LowOrEmptyStockItems = lowStock,
            ActiveWarehouseItems = items.Count,
            MoneyDiaryIncomeThisMonth = diary
            .Where(e => e.EntryType == MoneyDiaryEntryType.Income && e.Currency == PricingCurrency.SYP_New)
            .Sum(e => e.Amount),
            MoneyDiaryExpenseThisMonth = diary
            .Where(e => e.EntryType == MoneyDiaryEntryType.Expense && e.Currency == PricingCurrency.SYP_New)
            .Sum(e => e.Amount),
            MoneyDiaryIncomeUsdThisMonth = diary
            .Where(e => e.EntryType == MoneyDiaryEntryType.Income && e.Currency == PricingCurrency.USD)
            .Sum(e => e.Amount),
            MoneyDiaryExpenseUsdThisMonth = diary
            .Where(e => e.EntryType == MoneyDiaryEntryType.Expense && e.Currency == PricingCurrency.USD)
            .Sum(e => e.Amount),
            PayrollPendingCount = payroll.Count,
            PayrollPendingAmount = payroll.Sum(p => p.BaseAmount + p.Bonus - p.Deduction),
            ActivePayrollEmployees = employeeCount,
            PendingMaintenanceInvoices = pendingMaintenance.Count,
            PendingMaintenanceInvoicesAmount = pendingMaintenance.Sum(i => i.GrossAmount),
            CashBoxBalance = cashBox?.Balance ?? 0m,
            CashBoxBalanceUsd = cashBox?.BalanceUsd ?? 0m,
            UnpaidMaterialPurchaseTotal = materialPurchases.Sum(i => i.TotalAmount),
            UnpaidMaterialSalesTotal = materialSales.Sum(i => i.TotalAmount)
        };
    }
}
