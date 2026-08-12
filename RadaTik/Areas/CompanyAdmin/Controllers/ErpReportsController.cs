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
public sealed class ErpReportsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IErpReportService _reportService;

    public ErpReportsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IErpReportService reportService)
    {
        _context = context;
        _userManager = userManager;
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate)
    {
        ViewData["Title"] = "تقارير ERP";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        DateTime now = DateTime.UtcNow;
        DateTime from = fromDate?.Date ?? now.AddMonths(-3).Date;
        DateTime to = toDate?.Date ?? now.Date;

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.FromDate = from.ToString("yyyy-MM-dd");
        ViewBag.ToDate = to.ToString("yyyy-MM-dd");
        ViewBag.Reports = await _reportService.GetReportsAsync(scope.CompanyNetworkId, from, to);

        return View();
    }

    private async Task<CompanyBusinessScopeHelper.CompanyScope?> ResolveScopeAsync()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
        }

        return scope;
    }
}
