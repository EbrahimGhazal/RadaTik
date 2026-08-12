using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Models;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

/// <summary>عرض رواتب الشركة للموظفين المخوّلين (قراءة فقط).</summary>
[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Payroll)]
public class CompanyPayrollController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CompanyPayrollService _payrollService;
    private readonly IPermissionService _permissionService;

    public CompanyPayrollController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CompanyPayrollService payrollService,
        IPermissionService permissionService)
    {
        _context = context;
        _userManager = userManager;
        _payrollService = payrollService;
        _permissionService = permissionService;
    }

    [HttpGet]
    [RequirePermission("Payroll.View")]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        if (!await _permissionService.HasPermissionAsync(User, "Payroll.View"))
        {
            return Forbid();
        }

        ViewData["Title"] = "رواتب الشركة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        (int y, int m) = NormalizeYearMonth(year, month);
        PayrollIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.IsEmployeeReadOnly = true;
        ViewBag.CanManagePayroll = await _permissionService.HasPermissionAsync(User, "Payroll.Manage");
        return View(vm);
    }

    private static (int Year, int Month) NormalizeYearMonth(int? year, int? month)
    {
        DateTime now = DateTime.Now;
        int y = year is >= 2000 and <= 2100 ? year.Value : now.Year;
        int m = month is >= 1 and <= 12 ? month.Value : now.Month;
        return (y, m);
    }

    private async Task<PayrollIndexViewModel> LoadMonthViewModelAsync(int companyNetworkId, int year, int month)
    {
        List<PayrollPayment> payments = await _context.PayrollPayments
            .AsNoTracking()
            .Include(p => p.PayrollEmployee)
            .Where(p => p.CompanyNetworkId == companyNetworkId && p.Year == year && p.Month == month)
            .OrderBy(p => p.PayrollEmployee!.FullName)
            .ToListAsync();

        Dictionary<int, PayrollEmployee> employeesById = payments
            .Where(p => p.PayrollEmployee != null)
            .ToDictionary(p => p.PayrollEmployeeId, p => p.PayrollEmployee!);

        Dictionary<int, PayrollMonthLedger> ledgers =
            await _payrollService.BuildLedgersForPaymentsAsync(payments, employeesById);

        List<PayrollPaymentRowViewModel> rows = payments.Select(p =>
        {
            ledgers.TryGetValue(p.Id, out PayrollMonthLedger? ledger);
            ledger ??= new PayrollMonthLedger
            {
                AccruedBase = p.BaseAmount,
                PaymentBonus = p.Bonus,
                PaymentDeduction = p.Deduction
            };

            return new PayrollPaymentRowViewModel
            {
                Id = p.Id,
                PayrollEmployeeId = p.PayrollEmployeeId,
                EmployeeName = p.PayrollEmployee?.FullName ?? "",
                EmploymentLabel = p.PayrollEmployee != null
                    ? CompanyPayrollService.EmploymentTypeLabel(p.PayrollEmployee.EmploymentType)
                    : "",
                BaseAmount = p.BaseAmount,
                Bonus = p.Bonus,
                Deduction = p.Deduction,
                NetAmount = p.NetAmount,
                Withdrawals = ledger.Withdrawals,
                Advances = ledger.Advances,
                TransactionBonus = ledger.TransactionBonus,
                TransactionDeduction = ledger.TransactionDeduction,
                NetPayable = ledger.NetPayable,
                IsPaid = p.IsPaid,
                PaidAt = p.PaidAt
            };
        }).ToList();

        return new PayrollIndexViewModel
        {
            Year = year,
            Month = month,
            TotalNet = rows.Sum(r => r.NetAmount),
            TotalNetPayable = rows.Sum(r => r.NetPayable),
            TotalPaid = rows.Where(r => r.IsPaid).Sum(r => r.NetPayable),
            TotalPending = rows.Where(r => !r.IsPaid).Sum(r => r.NetPayable),
            Rows = rows
        };
    }
}
