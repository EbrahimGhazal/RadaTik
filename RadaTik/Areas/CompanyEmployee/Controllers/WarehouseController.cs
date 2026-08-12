using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Helpers;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Warehouse)]
public class WarehouseController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWarehouseStockService _warehouseStock;
    private readonly IPermissionService _permissionService;

    public WarehouseController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IWarehouseStockService warehouseStock,
        IPermissionService permissionService)
    {
        _context = context;
        _userManager = userManager;
        _warehouseStock = warehouseStock;
        _permissionService = permissionService;
    }

    [HttpGet]
    [RequirePermission("Warehouse.View")]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "المستودع";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        List<WarehouseItemRowViewModel> rows = await LoadItemRowsAsync(scope.CompanyNetworkId);
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Items = rows;
        ViewBag.LowStockCount = rows.Count(r => r.IsActive && r.OnHand <= 0m);
        ViewBag.CanManage = await _permissionService.HasPermissionAsync(User, "Warehouse.Manage");

        List<WarehouseMovement> recentMovements = await _context.WarehouseMovements
            .AsNoTracking()
            .Include(m => m.WarehouseItem)
            .Where(m => m.CompanyNetworkId == scope.CompanyNetworkId)
            .OrderByDescending(m => m.MovementDate)
            .ThenByDescending(m => m.Id)
            .Take(30)
            .ToListAsync();
        ViewBag.RecentMovements = recentMovements;
        return View();
    }

    private async Task<List<WarehouseItemRowViewModel>> LoadItemRowsAsync(int companyNetworkId)
    {
        Dictionary<int, decimal> onHandMap = await _warehouseStock.GetOnHandByItemIdAsync(companyNetworkId);
        List<WarehouseItem> items = await _context.WarehouseItems
            .AsNoTracking()
            .Where(i => i.CompanyNetworkId == companyNetworkId)
            .OrderBy(i => i.Name)
            .ToListAsync();

        return items.Select(i => new WarehouseItemRowViewModel
        {
            Id = i.Id,
            Name = i.Name,
            Unit = i.Unit,
            Sku = i.Sku,
            ModelNumber = i.ModelNumber,
            PurchasePrice = i.PurchasePrice,
            PurchaseCurrency = i.PurchaseCurrency,
            WholesalePrice = i.WholesalePrice,
            RetailPrice = i.RetailPrice,
            OnHand = onHandMap.GetValueOrDefault(i.Id, 0m),
            IsActive = i.IsActive
        }).ToList();
    }
}
