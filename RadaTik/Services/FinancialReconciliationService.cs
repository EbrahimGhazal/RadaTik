using Microsoft.EntityFrameworkCore;

using RadaTik.Data;

using RadaTik.Helpers;

using RadaTik.Models;

using RadaTik.Models.Business;



namespace RadaTik.Services;



public sealed class FinancialReconciliationSnapshot

{

    public decimal CashBoxBalance { get; init; }

    public decimal CashBoxBalanceUsd { get; init; }



    public decimal DiaryIncomeSypThisMonth { get; init; }

    public decimal DiaryExpenseSypThisMonth { get; init; }

    public decimal DiaryNetSypThisMonth => DiaryIncomeSypThisMonth - DiaryExpenseSypThisMonth;



    public decimal DiaryIncomeUsdThisMonth { get; init; }

    public decimal DiaryExpenseUsdThisMonth { get; init; }

    public decimal DiaryNetUsdThisMonth => DiaryIncomeUsdThisMonth - DiaryExpenseUsdThisMonth;



    public decimal DiaryIncomeThisMonth => DiaryIncomeSypThisMonth;

    public decimal DiaryExpenseThisMonth => DiaryExpenseSypThisMonth;

    public decimal DiaryNetThisMonth => DiaryNetSypThisMonth;



    public decimal UnpaidPurchaseTotalSyp { get; init; }

    public decimal UnpaidPurchaseTotalUsd { get; init; }

    public int UnpaidPurchaseCountSyp { get; init; }

    public int UnpaidPurchaseCountUsd { get; init; }



    public decimal UnpaidSalesTotalSyp { get; init; }

    public decimal UnpaidSalesTotalUsd { get; init; }

    public int UnpaidSalesCountSyp { get; init; }

    public int UnpaidSalesCountUsd { get; init; }



    public decimal MaterialPurchasePaidSypThisMonth { get; init; }

    public decimal MaterialPurchasePaidUsdThisMonth { get; init; }

    public decimal MaterialSalesPaidSypThisMonth { get; init; }

    public decimal MaterialSalesPaidUsdThisMonth { get; init; }



    public decimal UnpaidPurchaseTotal => UnpaidPurchaseTotalSyp + UnpaidPurchaseTotalUsd;

    public int UnpaidPurchaseCount => UnpaidPurchaseCountSyp + UnpaidPurchaseCountUsd;

    public decimal UnpaidSalesTotal => UnpaidSalesTotalSyp + UnpaidSalesTotalUsd;

    public int UnpaidSalesCount => UnpaidSalesCountSyp + UnpaidSalesCountUsd;

    public decimal MaterialPurchasePaidThisMonth => MaterialPurchasePaidSypThisMonth + MaterialPurchasePaidUsdThisMonth;

    public decimal MaterialSalesPaidThisMonth => MaterialSalesPaidSypThisMonth + MaterialSalesPaidUsdThisMonth;

}



public sealed class FinancialReconciliationService(ApplicationDbContext context)

{

    private readonly ApplicationDbContext _context = context;



    public async Task<FinancialReconciliationSnapshot> GetSnapshotAsync(

      int companyNetworkId,

      CancellationToken ct = default)

    {

        DateTime monthStart = new(DateTime.Today.Year, DateTime.Today.Month, 1);

        DateTime monthEnd = monthStart.AddMonths(1);



        CashBox? box = await CashBoxHelper.GetOrCreateCashBoxAsync(

          _context, CashBoxOwnerType.Network, companyNetworkId);

        decimal cashBalance = box?.Balance ?? 0m;

        decimal cashBalanceUsd = box?.BalanceUsd ?? 0m;



        List<MoneyDiaryEntry> diary = await _context.MoneyDiaryEntries

          .AsNoTracking()

          .Where(e => e.CompanyNetworkId == companyNetworkId

                      && e.EntryDate >= monthStart

                      && e.EntryDate < monthEnd)

          .ToListAsync(ct);



        List<MaterialPurchaseInvoice> purchases = await _context.MaterialPurchaseInvoices

          .AsNoTracking()

          .Where(i => i.CompanyNetworkId == companyNetworkId && !i.IsCancelled)

          .ToListAsync(ct);



        List<MaterialSalesInvoice> sales = await _context.MaterialSalesInvoices

          .AsNoTracking()

          .Where(i => i.CompanyNetworkId == companyNetworkId && !i.IsCancelled)

          .ToListAsync(ct);



        List<MaterialPurchaseInvoice> unpaidPurchases = purchases.Where(i => !i.IsPaid).ToList();

        List<MaterialSalesInvoice> unpaidSales = sales.Where(i => !i.IsPaid).ToList();



        List<MaterialPurchaseInvoice> paidPurchasesThisMonth = purchases

          .Where(i => i.IsPaid && i.InvoiceDate >= monthStart && i.InvoiceDate < monthEnd)

          .ToList();

        List<MaterialSalesInvoice> paidSalesThisMonth = sales

          .Where(i => i.IsPaid && i.InvoiceDate >= monthStart && i.InvoiceDate < monthEnd)

          .ToList();



        return new FinancialReconciliationSnapshot

        {

            CashBoxBalance = cashBalance,

            CashBoxBalanceUsd = cashBalanceUsd,

            DiaryIncomeSypThisMonth = diary

            .Where(e => e.EntryType == MoneyDiaryEntryType.Income && e.Currency == PricingCurrency.SYP_New)

            .Sum(e => e.Amount),

            DiaryExpenseSypThisMonth = diary

            .Where(e => e.EntryType == MoneyDiaryEntryType.Expense && e.Currency == PricingCurrency.SYP_New)

            .Sum(e => e.Amount),

            DiaryIncomeUsdThisMonth = diary

            .Where(e => e.EntryType == MoneyDiaryEntryType.Income && e.Currency == PricingCurrency.USD)

            .Sum(e => e.Amount),

            DiaryExpenseUsdThisMonth = diary

            .Where(e => e.EntryType == MoneyDiaryEntryType.Expense && e.Currency == PricingCurrency.USD)

            .Sum(e => e.Amount),

            UnpaidPurchaseTotalSyp = unpaidPurchases

            .Where(i => i.Currency == PricingCurrency.SYP_New)

            .Sum(i => i.TotalAmount),

            UnpaidPurchaseTotalUsd = unpaidPurchases

            .Where(i => i.Currency == PricingCurrency.USD)

            .Sum(i => i.TotalAmount),

            UnpaidPurchaseCountSyp = unpaidPurchases.Count(i => i.Currency == PricingCurrency.SYP_New),

            UnpaidPurchaseCountUsd = unpaidPurchases.Count(i => i.Currency == PricingCurrency.USD),

            UnpaidSalesTotalSyp = unpaidSales

            .Where(i => i.Currency == PricingCurrency.SYP_New)

            .Sum(i => i.TotalAmount),

            UnpaidSalesTotalUsd = unpaidSales

            .Where(i => i.Currency == PricingCurrency.USD)

            .Sum(i => i.TotalAmount),

            UnpaidSalesCountSyp = unpaidSales.Count(i => i.Currency == PricingCurrency.SYP_New),

            UnpaidSalesCountUsd = unpaidSales.Count(i => i.Currency == PricingCurrency.USD),

            MaterialPurchasePaidSypThisMonth = paidPurchasesThisMonth

            .Where(i => i.Currency == PricingCurrency.SYP_New)

            .Sum(i => i.TotalAmount),

            MaterialPurchasePaidUsdThisMonth = paidPurchasesThisMonth

            .Where(i => i.Currency == PricingCurrency.USD)

            .Sum(i => i.TotalAmount),

            MaterialSalesPaidSypThisMonth = paidSalesThisMonth

            .Where(i => i.Currency == PricingCurrency.SYP_New)

            .Sum(i => i.TotalAmount),

            MaterialSalesPaidUsdThisMonth = paidSalesThisMonth

            .Where(i => i.Currency == PricingCurrency.USD)

            .Sum(i => i.TotalAmount)

        };

    }

}

