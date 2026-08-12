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
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.MoneyDiary)]
public class FinancialReconciliationController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly FinancialReconciliationService _reconciliation;

    public FinancialReconciliationController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      FinancialReconciliationService reconciliation)
    {
        _context = context;
        _userManager = userManager;
        _reconciliation = reconciliation;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "جرد مالي";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Snapshot = await _reconciliation.GetSnapshotAsync(scope.CompanyNetworkId, ct);
        return View();
    }
}
