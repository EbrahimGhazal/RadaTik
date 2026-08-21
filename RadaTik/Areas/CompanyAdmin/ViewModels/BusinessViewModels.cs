using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.ViewModels;

public sealed class WarehouseItemRowViewModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string? Unit { get; init; }
    public string? Sku { get; init; }
    public string? ModelNumber { get; init; }
    public decimal? PurchasePrice { get; init; }
    public PricingCurrency? PurchaseCurrency { get; init; }
    public decimal? WholesalePrice { get; init; }
    public decimal? RetailPrice { get; init; }
    public decimal OnHand { get; init; }
    public bool IsActive { get; init; }
}

public sealed class MaterialPurchaseInvoiceFormViewModel
{
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public string? SupplierName { get; set; }
    public int? ErpSupplierId { get; set; }
    public PricingCurrency? Currency { get; set; }
    /// <summary>paid | unpaid — null عند أول فتح النموذج.</summary>
    public string? PaymentStatus { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<MaterialInvoiceLineInput> Lines { get; set; } = [];
    public IReadOnlyList<WarehouseItemRowViewModel> WarehouseItems { get; init; } = Array.Empty<WarehouseItemRowViewModel>();
}

public sealed class MaterialPurchaseInvoiceEditViewModel
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string? SupplierName { get; set; }
    public int? ErpSupplierId { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class MaterialSalesInvoiceFormViewModel
{
    public DateTime InvoiceDate { get; set; } = DateTime.Today;
    public string? CustomerName { get; set; }
    public int? ErpCustomerId { get; set; }
    public PricingCurrency? Currency { get; set; }
    /// <summary>paid | unpaid — null عند أول فتح النموذج.</summary>
    public string? PaymentStatus { get; set; }
    public MaterialSalePriceMode? PriceMode { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<WarehouseItemRowViewModel> WarehouseItems { get; init; } = Array.Empty<WarehouseItemRowViewModel>();
}

public sealed class MaterialSalesInvoiceEditViewModel
{
    public int Id { get; set; }
    public DateTime InvoiceDate { get; set; }
    public string? CustomerName { get; set; }
    public int? ErpCustomerId { get; set; }
    public bool IsPaid { get; set; }
    public string? Notes { get; set; }
    public decimal TotalAmount { get; set; }
}

public sealed class WarehouseStocktakeFormViewModel
{
    public DateTime StocktakeDate { get; set; } = DateTime.Today;
    public DateTime? PeriodFrom { get; set; }
    public DateTime? PeriodTo { get; set; }
    public int? WarehouseItemId { get; set; }
    public string? Notes { get; set; }
    public IReadOnlyList<WarehouseStocktakeRowViewModel> Rows { get; init; } = Array.Empty<WarehouseStocktakeRowViewModel>();
}

public sealed class WarehouseStocktakeRowViewModel
{
    public int WarehouseItemId { get; init; }
    public string Name { get; init; } = "";
    public string? ModelNumber { get; init; }
    public decimal SystemQuantity { get; init; }
}

public sealed class WarehouseInventoryReportRowViewModel
{
    public int WarehouseItemId { get; init; }
    public string Name { get; init; } = "";
    public string? ModelNumber { get; init; }
    public decimal OpeningQuantity { get; init; }
    public decimal InQuantity { get; init; }
    public decimal OutQuantity { get; init; }
    public decimal AdjustmentQuantity { get; init; }
    public decimal ClosingQuantity { get; init; }
}

public sealed class MoneyDiaryIndexViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TotalIncomeSyp { get; init; }
    public decimal TotalExpenseSyp { get; init; }
    public decimal TotalIncomeUsd { get; init; }
    public decimal TotalExpenseUsd { get; init; }
    public decimal NetSyp => TotalIncomeSyp - TotalExpenseSyp;
    public decimal NetUsd => TotalIncomeUsd - TotalExpenseUsd;
    public IReadOnlyList<MoneyDiaryEntry> Entries { get; init; } = Array.Empty<MoneyDiaryEntry>();
}

public sealed class PayrollIndexViewModel
{
    public int Year { get; init; }
    public int Month { get; init; }
    public decimal TotalNet { get; init; }
    public decimal TotalNetPayable { get; init; }
    public decimal TotalPaid { get; init; }
    public decimal TotalPending { get; init; }
    public IReadOnlyList<PayrollPaymentRowViewModel> Rows { get; init; } = Array.Empty<PayrollPaymentRowViewModel>();
}

public sealed class PayrollPaymentRowViewModel
{
    public int Id { get; init; }
    public int PayrollEmployeeId { get; init; }
    public string EmployeeName { get; init; } = "";
    public string EmploymentLabel { get; init; } = "";
    public decimal BaseAmount { get; init; }
    public decimal Bonus { get; init; }
    public decimal Deduction { get; init; }
    public decimal NetAmount { get; init; }
    public decimal Withdrawals { get; init; }
    public decimal Advances { get; init; }
    public decimal TransactionBonus { get; init; }
    public decimal TransactionDeduction { get; init; }
    public decimal NetPayable { get; init; }
    public bool IsPaid { get; init; }
    public DateTime? PaidAt { get; init; }
}

public sealed class PayrollSystemUserOptionViewModel
{
    public string Id { get; init; } = "";
    public string DisplayName { get; init; } = "";
}

public sealed class PayrollTransactionRowViewModel
{
    public int Id { get; init; }
    public PayrollTransactionType Type { get; init; }
    public string TypeLabel { get; init; } = "";
    public decimal Amount { get; init; }
    public DateTime TransactionDate { get; init; }
    public string? Notes { get; init; }
}

public sealed class PayrollSalaryRevisionRowViewModel
{
    public DateTime EffectiveDate { get; init; }
    public decimal PreviousSalary { get; init; }
    public decimal NewSalary { get; init; }
    public string AdjustmentDescription { get; init; } = "";
    public string? Notes { get; init; }
}

public sealed class PayrollEmployeeDetailsViewModel
{
    public int EmployeeId { get; init; }
    public string FullName { get; init; } = "";
    public string? JobTitle { get; init; }
    public string? Phone { get; init; }
    public PayrollEmploymentType EmploymentType { get; init; }
    public string EmploymentLabel { get; init; } = "";
    public decimal WeeklyWorkHours { get; init; }
    public decimal MonthlySalary { get; init; }
    public DateTime? HireDate { get; init; }
    public bool IsActive { get; init; }
    public string? LinkedUserName { get; init; }
    public string? LinkedApplicationUserId { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public PayrollMonthLedgerSummaryViewModel MonthSummary { get; init; } = new();
    public PayrollPayment? MonthPayment { get; init; }
    public IReadOnlyList<PayrollTransactionRowViewModel> Transactions { get; init; } = Array.Empty<PayrollTransactionRowViewModel>();
    public IReadOnlyList<PayrollSalaryRevisionRowViewModel> SalaryRevisions { get; init; } = Array.Empty<PayrollSalaryRevisionRowViewModel>();
}

public sealed class PayrollMonthLedgerSummaryViewModel
{
    public decimal AccruedBase { get; init; }
    public decimal PaymentBonus { get; init; }
    public decimal PaymentDeduction { get; init; }
    public decimal TransactionBonus { get; init; }
    public decimal TransactionDeduction { get; init; }
    public decimal Withdrawals { get; init; }
    public decimal Advances { get; init; }
    public decimal NetPayable { get; init; }
}
