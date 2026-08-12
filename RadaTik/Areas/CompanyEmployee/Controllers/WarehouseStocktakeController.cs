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
public class WarehouseStocktakeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WarehouseStocktakeController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    [RequirePermission("WarehouseStocktake.Manage")]
    public async Task<IActionResult> Index()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        ViewData["Title"] = "جرد المستودع";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Stocktakes = await _context.WarehouseStocktakes
            .AsNoTracking()
            .Include(s => s.CreatedByUser)
            .Where(s => s.CompanyNetworkId == scope.CompanyNetworkId)
            .OrderByDescending(s => s.StocktakeDate)
            .ThenByDescending(s => s.Id)
            .Take(50)
            .ToListAsync();

        return View();
    }
}
