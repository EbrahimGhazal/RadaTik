using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

/// <summary>بوابة موحّدة لإدارة الشركة — عرض ملخص فقط دون خلط البيانات.</summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class CompanyBusinessController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFeatureAccessService _featureAccess;
    private readonly ICompanyBusinessSummaryService _summaryService;

    public CompanyBusinessController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IFeatureAccessService featureAccess,
      ICompanyBusinessSummaryService summaryService)
    {
        _context = context;
        _userManager = userManager;
        _featureAccess = featureAccess;
        _summaryService = summaryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "إدارة الشركة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        bool canWarehouse = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Warehouse);
        bool canMoneyDiary = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.MoneyDiary);
        bool canPayroll = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Payroll);

        if (!canWarehouse && !canMoneyDiary && !canPayroll)
        {
            TempData["Info"] = "فعّل خدمات «المستودع» أو «دفتر الإيراد والمصروف» أو «رواتب الموظفين» من صفحة الخدمات المتاحة.";
            return RedirectToAction("Index", "Features");
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.CanWarehouse = canWarehouse;
        ViewBag.CanMoneyDiary = canMoneyDiary;
        ViewBag.CanPayroll = canPayroll;
        ViewBag.Summary = await _summaryService.GetSummaryAsync(scope.CompanyNetworkId);

        return View();
    }
}
