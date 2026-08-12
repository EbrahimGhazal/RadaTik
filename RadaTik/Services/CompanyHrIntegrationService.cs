using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Security;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

/// <summary>
/// ربط موظفي النظام بسجل الرواتب.
/// </summary>
public sealed class CompanyHrIntegrationService
{
    private readonly ApplicationDbContext _context;

    public CompanyHrIntegrationService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PayrollEmployee?> GetPayrollForUserAsync(
        int companyNetworkId,
        string applicationUserId,
        CancellationToken cancellationToken = default)
    {
        return await _context.PayrollEmployees
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.CompanyNetworkId == companyNetworkId && e.ApplicationUserId == applicationUserId,
                cancellationToken);
    }

    public async Task<int> SyncUnlinkedSystemEmployeesAsync(
        int companyNetworkId,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        List<int> networkIds = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync(cancellationToken);

        HashSet<string> alreadyLinked = (await _context.PayrollEmployees
            .AsNoTracking()
            .Where(e => e.CompanyNetworkId == companyNetworkId && e.ApplicationUserId != null)
            .Select(e => e.ApplicationUserId!)
            .ToListAsync(cancellationToken)).ToHashSet();

        List<ApplicationUser> users = await _context.Users
            .Where(u => u.NetworkId != null && networkIds.Contains(u.NetworkId.Value) && u.IsActive)
            .ToListAsync(cancellationToken);

        int created = 0;
        foreach (ApplicationUser user in users)
        {
            if (alreadyLinked.Contains(user.Id))
            {
                continue;
            }

            IList<string> roles = await userManager.GetRolesAsync(user);
            if (!roles.Any(r => r == RoleNames.CompanyEmployee || r == RoleNames.EmployeeLegacy))
            {
                continue;
            }

            await EnsurePayrollRecordForUserAsync(user, companyNetworkId, cancellationToken: cancellationToken);
            created++;
        }

        return created;
    }

    public async Task<PayrollEmployee?> EnsurePayrollRecordForUserAsync(
        ApplicationUser user,
        int companyNetworkId,
        decimal? monthlySalary = null,
        PayrollEmploymentType employmentType = PayrollEmploymentType.FullTime,
        decimal? weeklyWorkHours = null,
        string? jobTitle = null,
        CancellationToken cancellationToken = default)
    {
        PayrollEmployee? existing = await _context.PayrollEmployees
            .FirstOrDefaultAsync(
                e => e.CompanyNetworkId == companyNetworkId && e.ApplicationUserId == user.Id,
                cancellationToken);
        if (existing != null)
        {
            return existing;
        }

        bool nameTaken = await _context.PayrollEmployees.AnyAsync(
            e => e.CompanyNetworkId == companyNetworkId && e.FullName == (user.FullName ?? user.UserName ?? "موظف"),
            cancellationToken);

        PayrollEmployee employee = new()
        {
            CompanyNetworkId = companyNetworkId,
            ApplicationUserId = user.Id,
            FullName = nameTaken
                ? $"{user.FullName ?? user.UserName} ({user.UserName})"
                : (user.FullName ?? user.UserName ?? "موظف"),
            JobTitle = jobTitle,
            Phone = user.PhoneNumber,
            EmploymentType = employmentType,
            WeeklyWorkHours = employmentType == PayrollEmploymentType.FullTime
                ? PayrollEmployee.FullTimeWeeklyHoursDefault
                : weeklyWorkHours ?? 20m,
            MonthlySalary = monthlySalary ?? 0m,
            HireDate = user.CreatedDate.Date,
            IsActive = user.IsActive
        };

        _context.PayrollEmployees.Add(employee);
        await _context.SaveChangesAsync(cancellationToken);
        return employee;
    }
}
