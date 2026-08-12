using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Models.Business;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyEmployee.ViewModels;

public sealed class PayrollWithdrawalRequestRowViewModel
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public PayrollWithdrawalRequestStatus Status { get; init; }
    public string StatusLabel { get; init; } = "";
    public string? Notes { get; init; }
    public string? ReviewNotes { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? ReviewedAt { get; init; }
    public int Year { get; init; }
    public int Month { get; init; }
    public bool CanCancel { get; init; }
}

public sealed class EmployeeWalletTopUpRequestRowViewModel
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public decimal PlatformCommissionAmount { get; init; }
    public EmployeeWalletTopUpRequestStatus Status { get; init; }
    public string StatusLabel { get; init; } = "";
    public string? Notes { get; init; }
    public DateTime RequestedAt { get; init; }
    public bool CanCancel { get; init; }
}

public sealed class EmployeeWalletTransactionRowViewModel
{
    public int Id { get; init; }
    public decimal Amount { get; init; }
    public decimal NewBalance { get; init; }
    public string SourceLabel { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public string? Notes { get; init; }
}

public sealed class EmployeeMyPayrollPageViewModel
{
    public required PayrollEmployeeDetailsViewModel Details { get; init; }
    public string CompanyName { get; init; } = "";
    public decimal WalletBalance { get; init; }
    public bool CanRequestWalletTopUp { get; init; }
    public decimal OutstandingNetPayable { get; init; }
    public decimal AvailableWithdrawal { get; init; }
    public PayrollMonthEmploymentPeriod EmploymentPeriod { get; init; } = new() { Year = DateTime.Today.Year, Month = DateTime.Today.Month, DaysInMonth = 30 };
    public DateTime? TerminationDate { get; init; }
    public IReadOnlyList<PayrollWithdrawalRequestRowViewModel> WithdrawalRequests { get; init; } = [];
    public IReadOnlyList<EmployeeWalletTopUpRequestRowViewModel> WalletTopUpRequests { get; init; } = [];
    public IReadOnlyList<EmployeeWalletTransactionRowViewModel> WalletTransactions { get; init; } = [];
    public IReadOnlyList<PayrollPayment> RecentPayments { get; init; } = [];
}
