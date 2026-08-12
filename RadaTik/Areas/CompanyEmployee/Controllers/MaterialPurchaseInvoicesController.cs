using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Helpers;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Warehouse)]
public class MaterialPurchaseInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MaterialPurchaseInvoicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    [RequirePermission("MaterialPurchase.View")]
    public async Task<IActionResult> Index()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["Title"] = "فواتير شراء المواد";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Invoices = await _context.MaterialPurchaseInvoices
            .AsNoTracking()
            .Where(i => i.CompanyNetworkId == scope.CompanyNetworkId)
            .OrderByDescending(i => i.InvoiceDate)
            .ThenByDescending(i => i.Id)
            .Take(100)
            .ToListAsync();

        return View();
    }
}
