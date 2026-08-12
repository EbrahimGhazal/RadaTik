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

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Erp)]
public class ErpHubController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFeatureAccessService _featureAccess;
    private readonly IErpSummaryService _summaryService;

    public ErpHubController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IFeatureAccessService featureAccess,
        IErpSummaryService summaryService)
    {
        _context = context;
        _userManager = userManager;
        _featureAccess = featureAccess;
        _summaryService = summaryService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "نظام ERP";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Summary = await _summaryService.GetSummaryAsync(scope.CompanyNetworkId);
        ViewBag.CanUsers = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Users);
        ViewBag.CanClients = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Clients);
        ViewBag.CanWarehouse = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Warehouse);
        ViewBag.CanMoneyDiary = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.MoneyDiary);
        ViewBag.CanPayroll = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Payroll);

        return View();
    }
}
