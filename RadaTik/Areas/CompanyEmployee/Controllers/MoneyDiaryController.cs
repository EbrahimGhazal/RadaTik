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

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.MoneyDiary)]
public class MoneyDiaryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FinancialReconciliationService _reconciliation;
    private readonly IPermissionService _permissionService;

    public MoneyDiaryController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        FinancialReconciliationService reconciliation,
        IPermissionService permissionService)
    {
        _context = context;
        _userManager = userManager;
        _reconciliation = reconciliation;
        _permissionService = permissionService;
    }

    [HttpGet]
    [RequirePermission("MoneyDiary.View")]
    public async Task<IActionResult> Index(int? year, int? month)
    {
        ViewData["Title"] = "دفتر الإيراد والمصروف";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        (int y, int m) = NormalizeYearMonth(year, month);
        MoneyDiaryIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.CanManage = await _permissionService.HasPermissionAsync(User, "MoneyDiary.Manage");
        return View(vm);
    }

    [HttpGet]
    [RequirePermission("FinancialReconciliation.View")]
    public async Task<IActionResult> Reconciliation(CancellationToken ct)
    {
        ViewData["Title"] = "جرد مالي";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Snapshot = await _reconciliation.GetSnapshotAsync(scope.CompanyNetworkId, ct);
        return View();
    }

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

    private async Task<MoneyDiaryIndexViewModel> LoadMonthViewModelAsync(int companyNetworkId, int year, int month)
    {
        DateTime from = new DateTime(year, month, 1);
        DateTime to = from.AddMonths(1);
        List<MoneyDiaryEntry> entries = await _context.MoneyDiaryEntries
            .AsNoTracking()
            .Include(e => e.CreatedByUser)
            .Where(e => e.CompanyNetworkId == companyNetworkId && e.EntryDate >= from && e.EntryDate < to)
            .OrderByDescending(e => e.EntryDate)
            .ThenByDescending(e => e.Id)
            .ToListAsync();

        return new MoneyDiaryIndexViewModel
        {
            Year = year,
            Month = month,
            TotalIncomeSyp = SumByCurrency(entries, MoneyDiaryEntryType.Income, PricingCurrency.SYP_New),
            TotalExpenseSyp = SumByCurrency(entries, MoneyDiaryEntryType.Expense, PricingCurrency.SYP_New),
            TotalIncomeUsd = SumByCurrency(entries, MoneyDiaryEntryType.Income, PricingCurrency.USD),
            TotalExpenseUsd = SumByCurrency(entries, MoneyDiaryEntryType.Expense, PricingCurrency.USD),
            Entries = entries
        };
    }

    private static decimal SumByCurrency(
        IEnumerable<MoneyDiaryEntry> entries,
        MoneyDiaryEntryType type,
        PricingCurrency currency) =>
        entries.Where(e => e.EntryType == type && e.Currency == currency).Sum(e => e.Amount);
}
