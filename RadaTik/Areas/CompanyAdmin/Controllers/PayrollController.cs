using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Payroll)]
public class PayrollController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CompanyPayrollService _payrollService;
    private readonly ICompanyMoneyDiaryService _moneyDiaryService;
    private readonly CompanyHrIntegrationService _hrIntegrationService;
    private readonly PayrollWithdrawalRequestService _withdrawalRequestService;

    public PayrollController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CompanyPayrollService payrollService,
        ICompanyMoneyDiaryService moneyDiaryService,
        CompanyHrIntegrationService hrIntegrationService,
        PayrollWithdrawalRequestService withdrawalRequestService)
    {
        _context = context;
        _userManager = userManager;
        _payrollService = payrollService;
        _moneyDiaryService = moneyDiaryService;
        _hrIntegrationService = hrIntegrationService;
        _withdrawalRequestService = withdrawalRequestService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        ViewData["Title"] = "رواتب الموظفين";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        (int y, int m) = NormalizeYearMonth(year, month);
        PayrollIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.BusinessModuleTitle = "رواتب الموظفين";
        ViewBag.BusinessModuleHint =
            "إدارة الدوام، السحوبات، السلف، المكافآت، الحسومات، وزيادات الراتب. الصرف النهائي = الراتب + المكافآت − الحسومات − السحوبات − السلف.";
        ViewBag.PendingWithdrawalRequests = await _context.PayrollWithdrawalRequests
            .AsNoTracking()
            .Include(r => r.PayrollEmployee)
            .Where(r =>
                r.CompanyNetworkId == scope.CompanyNetworkId
                && r.Status == PayrollWithdrawalRequestStatus.Pending)
            .OrderByDescending(r => r.CreatedAt)
            .Take(30)
            .ToListAsync();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReviewWithdrawalRequest(
        int id,
        bool approve,
        string? reviewNotes,
        int? year,
        int? month)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        (bool success, string message) = await _withdrawalRequestService.ReviewAsync(
            id,
            scope.CompanyNetworkId,
            user?.Id ?? "",
            approve,
            reviewNotes);

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpGet]
    public async Task<IActionResult> Print(int? year, int? month)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        (int y, int m) = NormalizeYearMonth(year, month);
        PayrollIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.PrintTitle = "كشف رواتب الموظفين";
        return View("Print", vm);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(int? year, int? month)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        (int y, int m) = NormalizeYearMonth(year, month);
        PayrollIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);
        string fileName = CompanyBusinessExcelHelper.SanitizeFileName($"رواتب_{scope.CompanyNetworkName}_{y}_{m:D2}.xlsx");

        byte[] bytes = CompanyBusinessExcelHelper.BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = $"رواتب — {scope.CompanyNetworkName} — {y}/{m}";
            ws.Cell(2, 1).Value = $"صافي مستحق: {vm.TotalNetPayable:N2} | مُصروف: {vm.TotalPaid:N2} | معلّق: {vm.TotalPending:N2}";
            int row = 4;
            ws.Cell(row, 1).Value = "الموظف";
            ws.Cell(row, 2).Value = "أساسي";
            ws.Cell(row, 3).Value = "مكافآت";
            ws.Cell(row, 4).Value = "حسومات";
            ws.Cell(row, 5).Value = "سحوبات";
            ws.Cell(row, 6).Value = "سلف";
            ws.Cell(row, 7).Value = "المستحق";
            ws.Cell(row, 8).Value = "الحالة";
            ws.Row(row).Style.Font.Bold = true;
            row++;
            foreach (PayrollPaymentRowViewModel r in vm.Rows)
            {
                ws.Cell(row, 1).Value = r.EmployeeName;
                ws.Cell(row, 2).Value = r.BaseAmount;
                ws.Cell(row, 3).Value = r.Bonus + r.TransactionBonus;
                ws.Cell(row, 4).Value = r.Deduction + r.TransactionDeduction;
                ws.Cell(row, 5).Value = r.Withdrawals;
                ws.Cell(row, 6).Value = r.Advances;
                ws.Cell(row, 7).Value = r.NetPayable;
                ws.Cell(row, 8).Value = r.IsPaid ? "مُصروف" : "معلق";
                row++;
            }
        });

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Employees()
    {
        ViewData["Title"] = "إدارة موظفي الشركة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        List<PayrollSystemUserOptionViewModel> linkableUsers =
            await LoadLinkableSystemUsersAsync(scope.CompanyNetworkId, null);
        ViewBag.LinkableUsers = linkableUsers;
        ViewBag.UnlinkedSystemEmployeeCount = linkableUsers.Count;

        List<PayrollEmployee> employees = await _context.PayrollEmployees
            .AsNoTracking()
            .Include(e => e.ApplicationUser)
            .Where(e => e.CompanyNetworkId == scope.CompanyNetworkId)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        return View(employees);
    }

    [HttpGet]
    public async Task<IActionResult> EmployeeDetails(int id, int? year, int? month)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Employees));
        }

        PayrollEmployee? employee = await _context.PayrollEmployees
            .Include(e => e.ApplicationUser)
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyNetworkId == scope.CompanyNetworkId);
        if (employee == null)
        {
            return NotFound();
        }

        (int y, int m) = NormalizeYearMonth(year, month);
        ViewData["Title"] = $"ملف الموظف — {employee.FullName}";

        PayrollPayment? payment = await _context.PayrollPayments
            .FirstOrDefaultAsync(p =>
                p.PayrollEmployeeId == employee.Id && p.Year == y && p.Month == m);

        PayrollMonthLedger ledger = await _payrollService.BuildMonthLedgerAsync(employee, y, m, payment);

        List<PayrollTransaction> transactions = await _context.PayrollTransactions
            .AsNoTracking()
            .Where(t => t.PayrollEmployeeId == employee.Id && t.Year == y && t.Month == m)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        List<PayrollSalaryRevision> revisions = await _context.PayrollSalaryRevisions
            .AsNoTracking()
            .Where(r => r.PayrollEmployeeId == employee.Id)
            .OrderByDescending(r => r.EffectiveDate)
            .Take(20)
            .ToListAsync();

        PayrollEmployeeDetailsViewModel vm = new()
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
            LinkedUserName = employee.ApplicationUser?.UserName,
            LinkedApplicationUserId = employee.ApplicationUserId,
            Year = y,
            Month = m,
            MonthPayment = payment,
            MonthSummary = MapLedgerSummary(ledger),
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

        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncSystemEmployees()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Employees));
        }

        int created = await _hrIntegrationService.SyncUnlinkedSystemEmployeesAsync(
            scope.CompanyNetworkId,
            _userManager);
        TempData["Success"] = created > 0
            ? $"تم إنشاء {created} سجل رواتب مرتبطاً بحسابات النظام."
            : "لا يوجد موظفو نظام غير مربوطين — كل شيء محدّث.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(
        string fullName,
        string? jobTitle,
        string? phone,
        decimal monthlySalary,
        PayrollEmploymentType employmentType,
        decimal weeklyWorkHours,
        DateTime? hireDate,
        string? applicationUserId)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Employees));
        }

        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["Error"] = "اسم الموظف مطلوب.";
            return RedirectToAction(nameof(Employees));
        }

        if (monthlySalary < 0m)
        {
            TempData["Error"] = "الراتب لا يمكن أن يكون سالباً.";
            return RedirectToAction(nameof(Employees));
        }

        if (!await ValidateUserLinkAsync(scope.CompanyNetworkId, applicationUserId, null))
        {
            return RedirectToAction(nameof(Employees));
        }

        _context.PayrollEmployees.Add(new PayrollEmployee
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            FullName = fullName,
            JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim(),
            Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            MonthlySalary = monthlySalary,
            EmploymentType = employmentType,
            WeeklyWorkHours = NormalizeWeeklyHours(employmentType, weeklyWorkHours),
            HireDate = hireDate?.Date,
            ApplicationUserId = string.IsNullOrWhiteSpace(applicationUserId) ? null : applicationUserId
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = "تمت إضافة الموظف.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateEmployee(
        int id,
        string fullName,
        string? jobTitle,
        string? phone,
        decimal monthlySalary,
        PayrollEmploymentType employmentType,
        decimal weeklyWorkHours,
        DateTime? hireDate,
        string? applicationUserId)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Employees));
        }

        PayrollEmployee? emp = await _context.PayrollEmployees
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyNetworkId == scope.CompanyNetworkId);
        if (emp == null)
        {
            TempData["Error"] = "الموظف غير موجود.";
            return RedirectToAction(nameof(Employees));
        }

        fullName = (fullName ?? "").Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            TempData["Error"] = "اسم الموظف مطلوب.";
            return RedirectToAction(nameof(Employees));
        }

        if (!await ValidateUserLinkAsync(scope.CompanyNetworkId, applicationUserId, emp.Id))
        {
            return RedirectToAction(nameof(Employees));
        }

        emp.FullName = fullName;
        emp.JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle.Trim();
        emp.Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        emp.MonthlySalary = Math.Max(0m, monthlySalary);
        emp.EmploymentType = employmentType;
        emp.WeeklyWorkHours = NormalizeWeeklyHours(employmentType, weeklyWorkHours);
        emp.HireDate = hireDate?.Date;
        emp.ApplicationUserId = string.IsNullOrWhiteSpace(applicationUserId) ? null : applicationUserId;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تحديث بيانات الموظف.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmployee(int id, bool isActive)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Employees));
        }

        PayrollEmployee? emp = await _context.PayrollEmployees
            .FirstOrDefaultAsync(e => e.Id == id && e.CompanyNetworkId == scope.CompanyNetworkId);
        if (emp == null)
        {
            TempData["Error"] = "الموظف غير موجود.";
            return RedirectToAction(nameof(Employees));
        }

        emp.IsActive = isActive;
        if (!isActive)
        {
            emp.TerminationDate ??= DateTime.UtcNow.Date;
        }
        else
        {
            emp.TerminationDate = null;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = isActive ? "تم تفعيل الموظف." : "تم إيقاف الموظف.";
        return RedirectToAction(nameof(Employees));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddTransaction(
        int employeeId,
        PayrollTransactionType type,
        decimal amount,
        int year,
        int month,
        string? notes,
        DateTime? transactionDate)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId, year, month });
        }

        if (amount <= 0m)
        {
            TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
            return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId, year, month });
        }

        PayrollEmployee? employee = await _context.PayrollEmployees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.CompanyNetworkId == scope.CompanyNetworkId);
        if (employee == null)
        {
            TempData["Error"] = "الموظف غير موجود.";
            return RedirectToAction(nameof(Employees));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        _context.PayrollTransactions.Add(new PayrollTransaction
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            PayrollEmployeeId = employee.Id,
            Type = type,
            Amount = amount,
            Year = year,
            Month = month,
            TransactionDate = transactionDate?.Date ?? DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = $"تم تسجيل {CompanyPayrollService.TransactionTypeLabel(type)}.";
        return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId, year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplySalaryIncrease(
        int employeeId,
        PayrollSalaryAdjustmentType adjustmentType,
        decimal adjustmentValue,
        string? notes,
        DateTime? effectiveDate)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId });
        }

        if (adjustmentValue <= 0m)
        {
            TempData["Error"] = "قيمة الزيادة يجب أن تكون أكبر من صفر.";
            return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId });
        }

        PayrollEmployee? employee = await _context.PayrollEmployees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.CompanyNetworkId == scope.CompanyNetworkId);
        if (employee == null)
        {
            TempData["Error"] = "الموظف غير موجود.";
            return RedirectToAction(nameof(Employees));
        }

        decimal previous = employee.MonthlySalary;
        decimal updated = CompanyPayrollService.CalculateSalaryAfterAdjustment(
            previous, adjustmentType, adjustmentValue);

        if (updated <= previous)
        {
            TempData["Error"] = "الزيادة لا تغيّر الراتب — تحقق من القيمة.";
            return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId });
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        employee.MonthlySalary = updated;
        _context.PayrollSalaryRevisions.Add(new PayrollSalaryRevision
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            PayrollEmployeeId = employee.Id,
            PreviousSalary = previous,
            NewSalary = updated,
            AdjustmentType = adjustmentType,
            AdjustmentValue = adjustmentValue,
            EffectiveDate = effectiveDate?.Date ?? DateTime.UtcNow,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = user?.Id
        });
        await _context.SaveChangesAsync();

        TempData["Success"] = $"تم رفع الراتب من {previous:N0} إلى {updated:N0} ل.س.";
        return RedirectToAction(nameof(EmployeeDetails), new { id = employeeId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PrepareMonth(int year, int month)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        List<PayrollEmployee> employees = await _context.PayrollEmployees
            .Where(e => e.CompanyNetworkId == scope.CompanyNetworkId)
            .ToListAsync();

        HashSet<int> existingEmployeeIds = (await _context.PayrollPayments
            .Where(p => p.CompanyNetworkId == scope.CompanyNetworkId && p.Year == year && p.Month == month)
            .Select(p => p.PayrollEmployeeId)
            .ToListAsync()).ToHashSet();

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int added = 0;
        foreach (PayrollEmployee emp in employees)
        {
            if (existingEmployeeIds.Contains(emp.Id)
                || !CompanyPayrollService.WasEmployedInMonth(emp, year, month))
            {
                continue;
            }

            _context.PayrollPayments.Add(new PayrollPayment
            {
                CompanyNetworkId = scope.CompanyNetworkId,
                PayrollEmployeeId = emp.Id,
                Year = year,
                Month = month,
                BaseAmount = CompanyPayrollService.CalculateProratedMonthlyBase(emp, year, month),
                CreatedByUserId = user?.Id
            });
            added++;
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = added > 0
            ? $"تم تجهيز {added} سجل راتب للشهر."
            : "كل الموظفين النشطين لديهم سجل لهذا الشهر.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePayment(
        int id,
        decimal baseAmount,
        decimal bonus,
        decimal deduction,
        string? notes,
        int year,
        int month)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index), new { year, month });
        }

        PayrollPayment? payment = await _context.PayrollPayments
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyNetworkId == scope.CompanyNetworkId);
        if (payment == null)
        {
            TempData["Error"] = "السجل غير موجود.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        if (payment.IsPaid)
        {
            TempData["Error"] = "لا يمكن تعديل راتب مُصروف مسبقاً.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        payment.BaseAmount = Math.Max(0m, baseAmount);
        payment.Bonus = Math.Max(0m, bonus);
        payment.Deduction = Math.Max(0m, deduction);
        payment.Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حفظ التعديل.";
        return RedirectToAction(nameof(Index), new { year, month });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid(int id, int year, int month, bool postToMoneyDiary = false)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index), new { year, month });
        }

        PayrollPayment? payment = await _context.PayrollPayments
            .Include(p => p.PayrollEmployee)
            .FirstOrDefaultAsync(p => p.Id == id && p.CompanyNetworkId == scope.CompanyNetworkId);
        if (payment == null || payment.PayrollEmployee == null)
        {
            TempData["Error"] = "السجل غير موجود.";
            return RedirectToAction(nameof(Index), new { year, month });
        }

        PayrollMonthLedger ledger = await _payrollService.BuildMonthLedgerAsync(
            payment.PayrollEmployee, year, month, payment);

        payment.IsPaid = true;
        payment.PaidAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        string employeeName = payment.PayrollEmployee.FullName;
        string diaryDescription = $"راتب {employeeName} — {month}/{year}";

        if (postToMoneyDiary && ledger.NetPayable > 0m)
        {
            ApplicationUser? actor = await _userManager.GetUserAsync(User);
            bool posted = await _moneyDiaryService.TryPostSalaryExpenseAsync(
                scope.CompanyNetworkId,
                payment.Id,
                ledger.NetPayable,
                diaryDescription,
                actor?.Id);
            if (posted)
            {
                TempData["Success"] = $"تم تعليم راتب {employeeName} كمُصروف وتسجيله في دفتر النقدية.";
                return RedirectToAction(nameof(Index), new { year, month });
            }
        }

        TempData["Success"] = $"تم تعليم راتب {employeeName} كمُصروف.";
        TempData["SuggestMoneyDiaryAmount"] = ledger.NetPayable.ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
        TempData["SuggestMoneyDiaryDescription"] = diaryDescription;
        return RedirectToAction(nameof(Index), new { year, month });
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

    private async Task<List<PayrollSystemUserOptionViewModel>> LoadLinkableSystemUsersAsync(
        int companyNetworkId,
        int? excludePayrollEmployeeId)
    {
        List<int> networkIds = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync();

        HashSet<string> alreadyLinked = (await _context.PayrollEmployees
            .AsNoTracking()
            .Where(e => e.CompanyNetworkId == companyNetworkId
                        && e.ApplicationUserId != null
                        && (excludePayrollEmployeeId == null || e.Id != excludePayrollEmployeeId))
            .Select(e => e.ApplicationUserId!)
            .ToListAsync()).ToHashSet();

        List<ApplicationUser> users = await _context.Users
            .AsNoTracking()
            .Where(u => u.NetworkId != null && networkIds.Contains(u.NetworkId.Value) && u.IsActive)
            .OrderBy(u => u.FullName)
            .ThenBy(u => u.UserName)
            .ToListAsync();

        List<PayrollSystemUserOptionViewModel> result = [];
        foreach (ApplicationUser user in users)
        {
            if (alreadyLinked.Contains(user.Id))
            {
                continue;
            }

            IList<string> roles = await _userManager.GetRolesAsync(user);
            if (!roles.Any(r =>
                    r == RoleNames.CompanyEmployee || r == RoleNames.EmployeeLegacy))
            {
                continue;
            }

            result.Add(new PayrollSystemUserOptionViewModel
            {
                Id = user.Id,
                DisplayName = string.IsNullOrWhiteSpace(user.FullName)
                    ? user.UserName ?? user.Id
                    : $"{user.FullName} ({user.UserName})"
            });
        }

        return result;
    }

    private async Task<bool> ValidateUserLinkAsync(int companyNetworkId, string? applicationUserId, int? payrollEmployeeId)
    {
        if (string.IsNullOrWhiteSpace(applicationUserId))
        {
            return true;
        }

        bool taken = await _context.PayrollEmployees.AnyAsync(e =>
            e.CompanyNetworkId == companyNetworkId
            && e.ApplicationUserId == applicationUserId
            && (payrollEmployeeId == null || e.Id != payrollEmployeeId));
        if (taken)
        {
            TempData["Error"] = "حساب النظام مربوط بموظف آخر.";
            return false;
        }

        return true;
    }

    private static decimal NormalizeWeeklyHours(PayrollEmploymentType type, decimal weeklyWorkHours) =>
        type == PayrollEmploymentType.FullTime
            ? PayrollEmployee.FullTimeWeeklyHoursDefault
            : Math.Clamp(weeklyWorkHours, 1m, 168m);

    private static PayrollMonthLedgerSummaryViewModel MapLedgerSummary(PayrollMonthLedger ledger) => new()
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

    private static string FormatRevisionDescription(PayrollSalaryRevision revision) =>
        revision.AdjustmentType == PayrollSalaryAdjustmentType.Percentage
            ? $"زيادة {revision.AdjustmentValue:0.##}%"
            : $"زيادة {revision.AdjustmentValue:N0} ل.س";

    private static (int Year, int Month) NormalizeYearMonth(int? year, int? month)
    {
        int y = year ?? DateTime.Today.Year;
        int m = month ?? DateTime.Today.Month;
        if (m < 1)
        {
            m = 1;
        }

        if (m > 12)
        {
            m = 12;
        }

        return (y, m);
    }
}
