using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;
using RadaTik.Services;

namespace RadaTik.Helpers;

/// <summary>استعلامات راتب الموظف المرتبط بحساب الدخول (بوابة الموظف).</summary>
public static class EmployeePayrollSelfHelper
{
    public sealed record SelfPayrollContext(
        int CompanyNetworkId,
        string CompanyNetworkName,
        PayrollEmployee Employee);

    public static async Task<SelfPayrollContext?> ResolveSelfPayrollAsync(
        ApplicationDbContext context,
        ApplicationUser user,
        CancellationToken cancellationToken = default)
    {
        if (!user.NetworkId.HasValue)
        {
            return null;
        }

        Network? selected = await context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == user.NetworkId.Value, cancellationToken);
        if (selected == null)
        {
            return null;
        }

        int companyNetworkId = selected.ParentNetworkId ?? selected.Id;
        string companyName = selected.Name;
        if (selected.ParentNetworkId.HasValue)
        {
            companyName = await context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId)
                .Select(n => n.Name)
                .FirstOrDefaultAsync(cancellationToken) ?? companyName;
        }

        PayrollEmployee? employee = await context.PayrollEmployees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.CompanyNetworkId == companyNetworkId && e.ApplicationUserId == user.Id,
                cancellationToken);

        if (employee == null)
        {
            return null;
        }

        return new SelfPayrollContext(companyNetworkId, companyName, employee);
    }

    public static (int Year, int Month) NormalizeYearMonth(int? year, int? month)
    {
        DateTime now = DateTime.Now;
        int y = year is >= 2000 and <= 2100 ? year.Value : now.Year;
        int m = month is >= 1 and <= 12 ? month.Value : now.Month;
        return (y, m);
    }

    public static PayrollMonthLedgerSummaryViewModel MapLedgerSummary(PayrollMonthLedger ledger) => new()
    {
        AccruedBase = ledger.AccruedBase,
        PaymentBonus = ledger.PaymentBonus,
        PaymentDeduction = ledger.PaymentDeduction,
        TransactionBonus = ledger.TransactionBonus,
        TransactionDeduction = ledger.TransactionDeduction,
        Withdrawals = ledger.Withdrawals,
        Advances = ledger.Advances,
        NetPayable = ledger.NetPayable
    };

    /// <summary>مستحق الشهر الحالي (للهيدر) — لا يجمع أشهراً افتراضية بدون سجل دفعة.</summary>
    public static async Task<decimal> GetCurrentMonthNetPayableAsync(
        ApplicationDbContext context,
        CompanyPayrollService payrollService,
        PayrollEmployee employee,
        CancellationToken cancellationToken = default)
    {
        (int y, int m) = NormalizeYearMonth(null, null);
        PayrollPayment? payment = await context.PayrollPayments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.PayrollEmployeeId == employee.Id && p.Year == y && p.Month == m, cancellationToken);

        if (payment?.IsPaid == true)
        {
            return 0m;
        }

        PayrollMonthLedger ledger = await payrollService.BuildMonthLedgerAsync(employee, y, m, payment, cancellationToken);
        return Math.Max(0m, ledger.NetPayable);
    }

    /// <summary>مجموع المستحق غير المُصروف لدفعات مسجّلة فقط (بدون تكرار الراتب الأساسي لـ 12 شهراً).</summary>
    public static async Task<decimal> GetOutstandingNetPayableAsync(
        ApplicationDbContext context,
        CompanyPayrollService payrollService,
        PayrollEmployee employee,
        CancellationToken cancellationToken = default)
    {
        List<PayrollPayment> unpaidPayments = await context.PayrollPayments
            .AsNoTracking()
            .Where(p => p.PayrollEmployeeId == employee.Id && !p.IsPaid)
            .ToListAsync(cancellationToken);

        decimal total = 0m;
        foreach (PayrollPayment payment in unpaidPayments)
        {
            PayrollMonthLedger ledger = await payrollService.BuildMonthLedgerAsync(
                employee,
                payment.Year,
                payment.Month,
                payment,
                cancellationToken);
            total += Math.Max(0m, ledger.NetPayable);
        }

        if (unpaidPayments.Count == 0)
        {
            total = await GetCurrentMonthNetPayableAsync(context, payrollService, employee, cancellationToken);
        }

        return total;
    }
}
