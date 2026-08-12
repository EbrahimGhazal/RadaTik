using Microsoft.EntityFrameworkCore;

using RadaTik.Data;

using RadaTik.Models.Business;



namespace RadaTik.Services;



public sealed class PayrollMonthEmploymentPeriod

{

    public int Year { get; init; }

    public int Month { get; init; }

    public int DaysInMonth { get; init; }

    public int WorkedDays { get; init; }

    public DateOnly PeriodStart { get; init; }

    public DateOnly PeriodEnd { get; init; }

    public bool WasEmployed => WorkedDays > 0;

    public decimal ProrationFactor => DaysInMonth > 0 ? (decimal)WorkedDays / DaysInMonth : 0m;

}



public sealed class PayrollMonthLedger

{

    public decimal AccruedBase { get; init; }

    public decimal PaymentBonus { get; init; }

    public decimal PaymentDeduction { get; init; }

    public decimal TransactionBonus { get; init; }

    public decimal TransactionDeduction { get; init; }

    public decimal Withdrawals { get; init; }

    public decimal Advances { get; init; }



    public decimal GrossBeforeCashMovements =>

        AccruedBase + PaymentBonus + TransactionBonus - PaymentDeduction - TransactionDeduction;



    public decimal NetPayable => GrossBeforeCashMovements - Withdrawals - Advances;

}



public sealed class CompanyPayrollService

{

    private readonly ApplicationDbContext _context;



    public CompanyPayrollService(ApplicationDbContext context)

    {

        _context = context;

    }



    public static decimal GetAccruedMonthlyBase(PayrollEmployee employee)

    {

        if (employee.EmploymentType == PayrollEmploymentType.FullTime)

        {

            return employee.MonthlySalary;

        }



        decimal weeklyHours = employee.WeeklyWorkHours > 0m

            ? employee.WeeklyWorkHours

            : PayrollEmployee.FullTimeWeeklyHoursDefault;



        return Math.Round(

            employee.MonthlySalary * (weeklyHours / PayrollEmployee.FullTimeWeeklyHoursDefault),

            2,

            MidpointRounding.AwayFromZero);

    }



    public static PayrollMonthEmploymentPeriod GetEmploymentPeriod(PayrollEmployee employee, int year, int month)

    {

        int daysInMonth = DateTime.DaysInMonth(year, month);

        DateOnly monthStart = new(year, month, 1);

        DateOnly monthEnd = new(year, month, daysInMonth);



        DateOnly hire = employee.HireDate.HasValue

            ? DateOnly.FromDateTime(employee.HireDate.Value.Date)

            : monthStart;



        DateOnly effectiveEnd = monthEnd;

        if (employee.TerminationDate.HasValue)

        {

            DateOnly term = DateOnly.FromDateTime(employee.TerminationDate.Value.Date);

            if (term < effectiveEnd)

            {

                effectiveEnd = term;

            }

        }

        else if (!employee.IsActive)

        {

            DateOnly today = DateOnly.FromDateTime(DateTime.Today);

            if (today < monthStart)

            {

                effectiveEnd = monthStart.AddDays(-1);

            }

            else if (today <= monthEnd)

            {

                effectiveEnd = today;

            }

        }



        DateOnly periodStart = hire > monthStart ? hire : monthStart;

        DateOnly periodEnd = effectiveEnd < monthEnd ? effectiveEnd : monthEnd;



        int workedDays = periodStart <= periodEnd

            ? periodEnd.DayNumber - periodStart.DayNumber + 1

            : 0;



        return new PayrollMonthEmploymentPeriod

        {

            Year = year,

            Month = month,

            DaysInMonth = daysInMonth,

            WorkedDays = workedDays,

            PeriodStart = periodStart,

            PeriodEnd = periodEnd

        };

    }



    public static bool WasEmployedInMonth(PayrollEmployee employee, int year, int month) =>

        GetEmploymentPeriod(employee, year, month).WasEmployed;



    public static decimal CalculateProratedMonthlyBase(PayrollEmployee employee, int year, int month)

    {

        PayrollMonthEmploymentPeriod period = GetEmploymentPeriod(employee, year, month);

        if (!period.WasEmployed)

        {

            return 0m;

        }



        decimal fullBase = GetAccruedMonthlyBase(employee);

        return Math.Round(fullBase * period.ProrationFactor, 2, MidpointRounding.AwayFromZero);

    }



    public static decimal CalculateSalaryAfterAdjustment(

        decimal currentSalary,

        PayrollSalaryAdjustmentType adjustmentType,

        decimal adjustmentValue)

    {

        return adjustmentType switch

        {

            PayrollSalaryAdjustmentType.FixedAmount => Math.Max(0m, currentSalary + adjustmentValue),

            PayrollSalaryAdjustmentType.Percentage => Math.Max(

                0m,

                Math.Round(currentSalary * (1m + adjustmentValue / 100m), 2, MidpointRounding.AwayFromZero)),

            _ => currentSalary

        };

    }



    public static string EmploymentTypeLabel(PayrollEmploymentType type) =>

        type == PayrollEmploymentType.PartTime ? "دوام جزئي" : "دوام كامل";



    public static string TransactionTypeLabel(PayrollTransactionType type) => type switch

    {

        PayrollTransactionType.MidMonthWithdrawal => "سحب من الراتب",

        PayrollTransactionType.Advance => "سلفة",

        PayrollTransactionType.Bonus => "مكافأة",

        PayrollTransactionType.Deduction => "حسم",

        _ => type.ToString()

    };



    public static string WithdrawalRequestStatusLabel(PayrollWithdrawalRequestStatus status) => status switch

    {

        PayrollWithdrawalRequestStatus.Pending => "قيد الانتظار",

        PayrollWithdrawalRequestStatus.Approved => "مقبول",

        PayrollWithdrawalRequestStatus.Rejected => "مرفوض",

        PayrollWithdrawalRequestStatus.Cancelled => "ملغي",

        _ => status.ToString()

    };



    public async Task<PayrollMonthLedger> BuildMonthLedgerAsync(

        PayrollEmployee employee,

        int year,

        int month,

        PayrollPayment? payment,

        CancellationToken cancellationToken = default)

    {

        List<PayrollTransaction> transactions = await _context.PayrollTransactions

            .AsNoTracking()

            .Where(t => t.PayrollEmployeeId == employee.Id && t.Year == year && t.Month == month)

            .ToListAsync(cancellationToken);



        decimal accrued = payment?.BaseAmount ?? 0m;



        return new PayrollMonthLedger

        {

            AccruedBase = accrued,

            PaymentBonus = payment?.Bonus ?? 0m,

            PaymentDeduction = payment?.Deduction ?? 0m,

            TransactionBonus = SumByType(transactions, PayrollTransactionType.Bonus),

            TransactionDeduction = SumByType(transactions, PayrollTransactionType.Deduction),

            Withdrawals = SumByType(transactions, PayrollTransactionType.MidMonthWithdrawal),

            Advances = SumByType(transactions, PayrollTransactionType.Advance)

        };

    }



    public async Task<Dictionary<int, PayrollMonthLedger>> BuildLedgersForPaymentsAsync(

        IReadOnlyList<PayrollPayment> payments,

        IReadOnlyDictionary<int, PayrollEmployee> employeesById,

        CancellationToken cancellationToken = default)

    {

        if (payments.Count == 0)

        {

            return new Dictionary<int, PayrollMonthLedger>();

        }



        int year = payments[0].Year;

        int month = payments[0].Month;

        HashSet<int> employeeIds = payments.Select(p => p.PayrollEmployeeId).ToHashSet();



        List<PayrollTransaction> allTransactions = await _context.PayrollTransactions

            .AsNoTracking()

            .Where(t => employeeIds.Contains(t.PayrollEmployeeId) && t.Year == year && t.Month == month)

            .ToListAsync(cancellationToken);



        Dictionary<int, List<PayrollTransaction>> byEmployee = allTransactions

            .GroupBy(t => t.PayrollEmployeeId)

            .ToDictionary(g => g.Key, g => g.ToList());



        Dictionary<int, PayrollMonthLedger> result = new();

        foreach (PayrollPayment payment in payments)

        {

            employeesById.TryGetValue(payment.PayrollEmployeeId, out PayrollEmployee? employee);

            byEmployee.TryGetValue(payment.PayrollEmployeeId, out List<PayrollTransaction>? txs);

            txs ??= [];



            result[payment.Id] = new PayrollMonthLedger

            {

                AccruedBase = payment.BaseAmount,

                PaymentBonus = payment.Bonus,

                PaymentDeduction = payment.Deduction,

                TransactionBonus = SumByType(txs, PayrollTransactionType.Bonus),

                TransactionDeduction = SumByType(txs, PayrollTransactionType.Deduction),

                Withdrawals = SumByType(txs, PayrollTransactionType.MidMonthWithdrawal),

                Advances = SumByType(txs, PayrollTransactionType.Advance)

            };

        }



        return result;

    }



    private static decimal SumByType(IEnumerable<PayrollTransaction> transactions, PayrollTransactionType type) =>

        transactions.Where(t => t.Type == type).Sum(t => t.Amount);

}


