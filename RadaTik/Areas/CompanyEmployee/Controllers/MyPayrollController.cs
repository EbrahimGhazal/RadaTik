using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using RadaTik.Areas.CompanyAdmin.ViewModels;

using RadaTik.Areas.CompanyEmployee.ViewModels;

using global::RadaTik.Constants;

using global::RadaTik.Data;

using global::RadaTik.Helpers;

using global::RadaTik.Models;

using global::RadaTik.Models.Business;

using global::RadaTik.Security;

using global::RadaTik.Services;



namespace RadaTik.Areas.CompanyEmployee.Controllers;



/// <summary>عرض راتب الموظف ومحفظته الشخصية (مكافآت، حسميات، سلف، طلبات سحب).</summary>

[Area("CompanyEmployee")]

[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]

[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Payroll)]

public class MyPayrollController : Controller

{

    private readonly ApplicationDbContext _context;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly CompanyPayrollService _payrollService;

    private readonly CompanyHrIntegrationService _hrIntegrationService;

    private readonly PayrollWithdrawalRequestService _withdrawalRequestService;

    private readonly EmployeeWalletTopUpService _walletTopUpService;

    private readonly IPermissionService _permissionService;



    public MyPayrollController(

        ApplicationDbContext context,

        UserManager<ApplicationUser> userManager,

        CompanyPayrollService payrollService,

        CompanyHrIntegrationService hrIntegrationService,

        PayrollWithdrawalRequestService withdrawalRequestService,

        EmployeeWalletTopUpService walletTopUpService,

        IPermissionService permissionService)

    {

        _context = context;

        _userManager = userManager;

        _payrollService = payrollService;

        _hrIntegrationService = hrIntegrationService;

        _withdrawalRequestService = withdrawalRequestService;

        _walletTopUpService = walletTopUpService;

        _permissionService = permissionService;

    }



    [HttpGet]

    public async Task<IActionResult> Index(int? year, int? month)

    {

        ViewData["Title"] = "راتبي ومحفظتي";

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user == null)

        {

            return Challenge();

        }



        PayrollEmployee? employee = await ResolveEmployeeAsync(user);

        if (employee == null)

        {

            TempData["Error"] = "لا يوجد سجل رواتب مرتبط بحسابك. تواصل مع مدير الشركة لربطك من شاشة الرواتب.";

            return RedirectToAction("Index", "Dashboard");

        }



        (int y, int m) = EmployeePayrollSelfHelper.NormalizeYearMonth(year, month);

        EmployeeMyPayrollPageViewModel vm = await BuildPageViewModelAsync(employee, user, y, m);

        return View(vm);

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SubmitWithdrawalRequest(

        decimal amount,

        string? notes,

        int? year,

        int? month)

    {

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user == null)

        {

            return Challenge();

        }



        PayrollEmployee? employee = await ResolveEmployeeAsync(user);

        if (employee == null)

        {

            TempData["Error"] = "لا يوجد سجل رواتب.";

            return RedirectToAction(nameof(Index));

        }



        (int y, int m) = EmployeePayrollSelfHelper.NormalizeYearMonth(year, month);

        (bool success, string message) = await _withdrawalRequestService.SubmitAsync(

            employee,

            employee.CompanyNetworkId,

            user.Id,

            y,

            m,

            amount,

            notes);



        TempData[success ? "Success" : "Error"] = message;

        return RedirectToAction(nameof(Index), new { year = y, month = m });

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> CancelWithdrawalRequest(int requestId, int? year, int? month)

    {

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user == null)

        {

            return Challenge();

        }



        PayrollEmployee? employee = await ResolveEmployeeAsync(user);

        if (employee == null)

        {

            TempData["Error"] = "لا يوجد سجل رواتب.";

            return RedirectToAction(nameof(Index));

        }



        (bool success, string message) = await _withdrawalRequestService.CancelPendingAsync(

            requestId,

            employee.Id,

            user.Id);



        (int y, int m) = EmployeePayrollSelfHelper.NormalizeYearMonth(year, month);

        TempData[success ? "Success" : "Error"] = message;

        return RedirectToAction(nameof(Index), new { year = y, month = m });

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SubmitWalletTopUpRequest(decimal amount, string? notes)

    {

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user == null)

        {

            return Challenge();

        }



        if (!await _permissionService.HasPermissionAsync(User, "Payroll.WalletTopUp.Request"))

        {

            TempData["Error"] = "ليس لديك صلاحية طلب تغذية المحفظة.";

            return RedirectToAction(nameof(Index));

        }



        PayrollEmployee? employee = await ResolveEmployeeAsync(user);

        if (employee == null)

        {

            TempData["Error"] = "لا يوجد سجل رواتب.";

            return RedirectToAction(nameof(Index));

        }



        EmployeePayrollSelfHelper.SelfPayrollContext? self =

            await EmployeePayrollSelfHelper.ResolveSelfPayrollAsync(_context, user);

        if (self == null)

        {

            TempData["Error"] = "تعذر تحديد الشركة.";

            return RedirectToAction(nameof(Index));

        }



        EmployeeWalletTopUpOutcome outcome = await _walletTopUpService.SubmitRequestAsync(

            employee,

            self.CompanyNetworkId,

            user.Id,

            amount,

            EmployeeWalletTopUpRequestSource.EmployeeSelf,

            notes);



        TempData[outcome.Success ? "Success" : "Error"] = outcome.Message;

        return RedirectToAction(nameof(Index));

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> CancelWalletTopUpRequest(int requestId)

    {

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        if (user == null)

        {

            return Challenge();

        }



        PayrollEmployee? employee = await ResolveEmployeeAsync(user);

        if (employee == null)

        {

            TempData["Error"] = "لا يوجد سجل رواتب.";

            return RedirectToAction(nameof(Index));

        }



        EmployeeWalletTopUpOutcome outcome = await _walletTopUpService.CancelPendingAsync(

            requestId, employee.Id, user.Id);



        TempData[outcome.Success ? "Success" : "Error"] = outcome.Message;

        return RedirectToAction(nameof(Index));

    }



    private async Task<PayrollEmployee?> ResolveEmployeeAsync(ApplicationUser user)

    {

        EmployeePayrollSelfHelper.SelfPayrollContext? self = await EmployeePayrollSelfHelper.ResolveSelfPayrollAsync(_context, user);

        if (self == null)

        {

            CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(

                HttpContext, _context, _userManager, User);

            if (scope == null)

            {

                return null;

            }



            await _hrIntegrationService.EnsurePayrollRecordForUserAsync(user, scope.CompanyNetworkId);

            self = await EmployeePayrollSelfHelper.ResolveSelfPayrollAsync(_context, user);

        }



        if (self == null)

        {

            return null;

        }



        return await _context.PayrollEmployees

            .AsNoTracking()

            .FirstAsync(e => e.Id == self.Employee.Id);

    }



    private async Task<EmployeeMyPayrollPageViewModel> BuildPageViewModelAsync(

        PayrollEmployee employee,

        ApplicationUser user,

        int year,

        int month)

    {

        EmployeePayrollSelfHelper.SelfPayrollContext? self = await EmployeePayrollSelfHelper.ResolveSelfPayrollAsync(_context, user);



        PayrollPayment? payment = await _context.PayrollPayments

            .AsNoTracking()

            .FirstOrDefaultAsync(p => p.PayrollEmployeeId == employee.Id && p.Year == year && p.Month == month);



        PayrollMonthLedger ledger = await _payrollService.BuildMonthLedgerAsync(employee, year, month, payment);



        List<PayrollTransaction> transactions = await _context.PayrollTransactions

            .AsNoTracking()

            .Where(t => t.PayrollEmployeeId == employee.Id && t.Year == year && t.Month == month)

            .OrderByDescending(t => t.TransactionDate)

            .ToListAsync();



        List<PayrollSalaryRevision> revisions = await _context.PayrollSalaryRevisions

            .AsNoTracking()

            .Where(r => r.PayrollEmployeeId == employee.Id)

            .OrderByDescending(r => r.EffectiveDate)

            .Take(15)

            .ToListAsync();



        List<PayrollPayment> recentPayments = await _context.PayrollPayments

            .AsNoTracking()

            .Where(p => p.PayrollEmployeeId == employee.Id)

            .OrderByDescending(p => p.Year)

            .ThenByDescending(p => p.Month)

            .Take(12)

            .ToListAsync();



        List<PayrollWithdrawalRequest> withdrawalRequests = await _context.PayrollWithdrawalRequests

            .AsNoTracking()

            .Where(r => r.PayrollEmployeeId == employee.Id)

            .OrderByDescending(r => r.CreatedAt)

            .Take(20)

            .ToListAsync();



        List<EmployeeWalletTransaction> walletTransactions = await _context.EmployeeWalletTransactions

            .AsNoTracking()

            .Where(t => t.PayrollEmployeeId == employee.Id)

            .OrderByDescending(t => t.CreatedAt)

            .Take(20)

            .ToListAsync();



        List<EmployeeWalletTopUpRequest> walletTopUpRequests = await _context.EmployeeWalletTopUpRequests

            .AsNoTracking()

            .Where(r => r.PayrollEmployeeId == employee.Id)

            .OrderByDescending(r => r.RequestedAt)

            .Take(15)

            .ToListAsync();



        decimal outstanding = await EmployeePayrollSelfHelper.GetOutstandingNetPayableAsync(

            _context, _payrollService, employee);



        decimal availableWithdrawal = await _withdrawalRequestService.GetAvailableWithdrawalAmountAsync(

            employee, year, month);



        PayrollMonthEmploymentPeriod period = CompanyPayrollService.GetEmploymentPeriod(employee, year, month);

        bool canRequestWalletTopUp = await _permissionService.HasPermissionAsync(User, "Payroll.WalletTopUp.Request");



        PayrollEmployeeDetailsViewModel details = new()

        {

            EmployeeId = employee.Id,

            FullName = employee.FullName,

            JobTitle = employee.JobTitle,

            Phone = employee.Phone,

            EmploymentType = employee.EmploymentType,

            EmploymentLabel = CompanyPayrollService.EmploymentTypeLabel(employee.EmploymentType),

            WeeklyWorkHours = employee.WeeklyWorkHours,

            MonthlySalary = employee.MonthlySalary,

            HireDate = employee.HireDate,

            IsActive = employee.IsActive,

            LinkedUserName = user.UserName,

            LinkedApplicationUserId = user.Id,

            Year = year,

            Month = month,

            MonthPayment = payment,

            MonthSummary = EmployeePayrollSelfHelper.MapLedgerSummary(ledger),

            Transactions = transactions.Select(t => new PayrollTransactionRowViewModel

            {

                Id = t.Id,

                Type = t.Type,

                TypeLabel = CompanyPayrollService.TransactionTypeLabel(t.Type),

                Amount = t.Amount,

                TransactionDate = t.TransactionDate,

                Notes = t.Notes

            }).ToList(),

            SalaryRevisions = revisions.Select(r => new PayrollSalaryRevisionRowViewModel

            {

                EffectiveDate = r.EffectiveDate,

                PreviousSalary = r.PreviousSalary,

                NewSalary = r.NewSalary,

                AdjustmentDescription = FormatRevisionDescription(r),

                Notes = r.Notes

            }).ToList()

        };



        return new EmployeeMyPayrollPageViewModel

        {

            Details = details,

            CompanyName = self?.CompanyNetworkName ?? "",

            OutstandingNetPayable = outstanding,

            AvailableWithdrawal = availableWithdrawal,

            WalletBalance = employee.WalletBalance,

            CanRequestWalletTopUp = canRequestWalletTopUp,

            EmploymentPeriod = period,

            TerminationDate = employee.TerminationDate,

            RecentPayments = recentPayments,

            WithdrawalRequests = withdrawalRequests.Select(r => new PayrollWithdrawalRequestRowViewModel

            {

                Id = r.Id,

                Amount = r.Amount,

                Status = r.Status,

                StatusLabel = CompanyPayrollService.WithdrawalRequestStatusLabel(r.Status),

                Notes = r.Notes,

                ReviewNotes = r.ReviewNotes,

                CreatedAt = r.CreatedAt,

                ReviewedAt = r.ReviewedAt,

                Year = r.Year,

                Month = r.Month,

                CanCancel = r.Status == PayrollWithdrawalRequestStatus.Pending

            }).ToList(),

            WalletTopUpRequests = walletTopUpRequests.Select(r => new EmployeeWalletTopUpRequestRowViewModel

            {

                Id = r.Id,

                Amount = r.Amount,

                PlatformCommissionAmount = r.PlatformCommissionAmount,

                Status = r.Status,

                StatusLabel = PricingDisplay.EmployeeWalletTopUpRequestStatusLabel(r.Status),

                Notes = r.Notes,

                RequestedAt = r.RequestedAt,

                CanCancel = r.Status == EmployeeWalletTopUpRequestStatus.Pending

            }).ToList(),

            WalletTransactions = walletTransactions.Select(t => new EmployeeWalletTransactionRowViewModel

            {

                Id = t.Id,

                Amount = t.Amount,

                NewBalance = t.NewBalance,

                SourceLabel = PricingDisplay.EmployeeWalletTransactionSourceLabel(t.Source),

                CreatedAt = t.CreatedAt,

                Notes = t.Notes

            }).ToList()

        };

    }



    private static string FormatRevisionDescription(PayrollSalaryRevision revision) =>

        revision.AdjustmentType == PayrollSalaryAdjustmentType.Percentage

            ? $"زيادة {revision.AdjustmentValue:0.##}%"

            : $"زيادة {revision.AdjustmentValue:N0} ل.س";

}


