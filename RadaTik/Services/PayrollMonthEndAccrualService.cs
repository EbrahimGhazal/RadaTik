using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models.Business;

namespace RadaTik.Services;

/// <summary>تجهيز سجل الراتب الأساسي (مُناسَب) في آخر ساعة من الشهر. المكافآت والحسومات تبقى عبر حركات الشهر وتُحسب في كشف الراتب.</summary>
public sealed class PayrollMonthEndAccrualService
{
    private readonly ApplicationDbContext _context;

    public PayrollMonthEndAccrualService(ApplicationDbContext context)
    {
        _context = context;
    }

    public static bool IsMonthEndAccrualWindow(DateTime localNow)
    {
        int daysInMonth = DateTime.DaysInMonth(localNow.Year, localNow.Month);
        return localNow.Day == daysInMonth && localNow.Hour == 23;
    }

    public async Task<int> RunForCompanyMonthAsync(
        int companyNetworkId,
        int year,
        int month,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        bool alreadyRan = await _context.PayrollMonthAccrualRuns.AnyAsync(
            r => r.CompanyNetworkId == companyNetworkId && r.Year == year && r.Month == month,
            cancellationToken);
        if (alreadyRan)
        {
            return 0;
        }

        List<PayrollEmployee> employees = await _context.PayrollEmployees
            .Where(e => e.CompanyNetworkId == companyNetworkId)
            .ToListAsync(cancellationToken);

        HashSet<int> existingEmployeeIds = (await _context.PayrollPayments
            .Where(p => p.CompanyNetworkId == companyNetworkId && p.Year == year && p.Month == month)
            .Select(p => p.PayrollEmployeeId)
            .ToListAsync(cancellationToken)).ToHashSet();

        int processed = 0;
        foreach (PayrollEmployee employee in employees)
        {
            if (!CompanyPayrollService.WasEmployedInMonth(employee, year, month))
            {
                continue;
            }

            decimal proratedBase = CompanyPayrollService.CalculateProratedMonthlyBase(employee, year, month);

            if (existingEmployeeIds.Contains(employee.Id))
            {
                PayrollPayment? payment = await _context.PayrollPayments
                    .FirstOrDefaultAsync(
                        p => p.PayrollEmployeeId == employee.Id && p.Year == year && p.Month == month,
                        cancellationToken);
                if (payment == null || payment.IsPaid)
                {
                    continue;
                }

                payment.BaseAmount = proratedBase;
                processed++;
            }
            else
            {
                _context.PayrollPayments.Add(new PayrollPayment
                {
                    CompanyNetworkId = companyNetworkId,
                    PayrollEmployeeId = employee.Id,
                    Year = year,
                    Month = month,
                    BaseAmount = proratedBase,
                    CreatedByUserId = actorUserId
                });
                existingEmployeeIds.Add(employee.Id);
                processed++;
            }
        }

        _context.PayrollMonthAccrualRuns.Add(new PayrollMonthAccrualRun
        {
            CompanyNetworkId = companyNetworkId,
            Year = year,
            Month = month,
            EmployeesProcessed = processed,
            RunAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync(cancellationToken);
        return processed;
    }

    public async Task RunAllCompaniesIfDueAsync(CancellationToken cancellationToken = default)
    {
        DateTime now = DateTime.Now;
        if (!IsMonthEndAccrualWindow(now))
        {
            return;
        }

        List<int> companyIds = await _context.PayrollEmployees
            .Select(e => e.CompanyNetworkId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (int companyId in companyIds)
        {
            await RunForCompanyMonthAsync(companyId, now.Year, now.Month, null, cancellationToken);
        }
    }
}
