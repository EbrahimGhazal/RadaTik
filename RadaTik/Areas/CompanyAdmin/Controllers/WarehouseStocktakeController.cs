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
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Warehouse)]
public class WarehouseStocktakeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWarehouseMaterialInvoiceService _invoiceService;
    private readonly IWarehouseStockService _warehouseStock;

    public WarehouseStocktakeController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IWarehouseMaterialInvoiceService invoiceService,
      IWarehouseStockService warehouseStock)
    {
        _context = context;
        _userManager = userManager;
        _invoiceService = invoiceService;
        _warehouseStock = warehouseStock;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewData["Title"] = "سجل الجرد";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Stocktakes = await _context.WarehouseStocktakes
          .AsNoTracking()
          .Include(s => s.Lines)
          .Include(s => s.WarehouseItem)
          .Include(s => s.CreatedByUser)
          .Where(s => s.CompanyNetworkId == scope.CompanyNetworkId)
          .OrderByDescending(s => s.StocktakeDate)
          .ThenByDescending(s => s.Id)
          .Take(80)
          .ToListAsync();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? warehouseItemId)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewData["Title"] = "جرد المستودع";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(await BuildFormAsync(scope.CompanyNetworkId, warehouseItemId));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
      DateTime stocktakeDate,
      DateTime? periodFrom,
      DateTime? periodTo,
      int? warehouseItemId,
      string? notes,
      List<StocktakeLineInput>? lines)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        MaterialInvoiceResult result = await _invoiceService.ApplyStocktakeAsync(
          scope.CompanyNetworkId,
          user?.Id,
          stocktakeDate,
          periodFrom,
          periodTo,
          warehouseItemId,
          notes,
          lines ?? [],
          HttpContext.RequestAborted);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            WarehouseStocktakeFormViewModel vm = await BuildFormAsync(scope.CompanyNetworkId, warehouseItemId);
            vm.StocktakeDate = stocktakeDate;
            vm.PeriodFrom = periodFrom;
            vm.PeriodTo = periodTo;
            vm.Notes = notes;
            return View(vm);
        }

        TempData["Success"] = "تم اعتماد الجرد وتسجيل فروقات التصحيح.";
        return RedirectToAction(nameof(Details), new { id = result.InvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        WarehouseStocktake? stocktake = await _context.WarehouseStocktakes
          .AsNoTracking()
          .Include(s => s.Lines)
          .ThenInclude(l => l.WarehouseItem)
          .Include(s => s.WarehouseItem)
          .Include(s => s.CreatedByUser)
          .FirstOrDefaultAsync(s => s.Id == id && s.CompanyNetworkId == scope.CompanyNetworkId);
        if (stocktake == null)
        {
            TempData["Error"] = "الجرد غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"جرد #{stocktake.Id}";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(stocktake);
    }

    [HttpGet]
    public async Task<IActionResult> Report(DateTime? from, DateTime? to, int? warehouseItemId)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        DateTime periodTo = (to ?? DateTime.Today).Date;
        DateTime periodFrom = (from ?? periodTo.AddDays(-30)).Date;
        if (periodFrom > periodTo)
        {
            (periodFrom, periodTo) = (periodTo, periodFrom);
        }

        DateTime openingDate = periodFrom.AddDays(-1);

        List<WarehouseMovement> movements = await _context.WarehouseMovements
          .AsNoTracking()
          .Where(m => m.CompanyNetworkId == scope.CompanyNetworkId)
          .ToListAsync();

        List<WarehouseItem> items = await _context.WarehouseItems
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == scope.CompanyNetworkId && i.IsActive)
          .Where(i => !warehouseItemId.HasValue || i.Id == warehouseItemId.Value)
          .OrderBy(i => i.Name)
          .ToListAsync();

        List<WarehouseInventoryReportRowViewModel> rows = items.Select(item =>
        {
            IEnumerable<WarehouseMovement> itemMovements = movements.Where(m => m.WarehouseItemId == item.Id);
            decimal opening = _warehouseStock.ComputeOnHand(itemMovements, openingDate);
            IEnumerable<WarehouseMovement> inPeriod = itemMovements.Where(m =>
          m.MovementDate.Date >= periodFrom && m.MovementDate.Date <= periodTo);

            decimal ins = 0m, outs = 0m, adj = 0m;
            foreach (WarehouseMovement m in inPeriod)
            {
                switch (m.MovementType)
                {
                    case WarehouseMovementType.In:
                        ins += m.Quantity;
                        break;
                    case WarehouseMovementType.Out:
                        outs += m.Quantity;
                        break;
                    case WarehouseMovementType.Adjustment:
                        adj += m.Quantity;
                        break;
                }
            }

            return new WarehouseInventoryReportRowViewModel
            {
                WarehouseItemId = item.Id,
                Name = item.Name,
                ModelNumber = item.ModelNumber,
                OpeningQuantity = opening,
                InQuantity = ins,
                OutQuantity = outs,
                AdjustmentQuantity = adj,
                ClosingQuantity = _warehouseStock.ComputeOnHand(itemMovements, periodTo)
            };
        }).ToList();

        ViewData["Title"] = "تقرير حركة المخزون";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.PeriodFrom = periodFrom;
        ViewBag.PeriodTo = periodTo;
        ViewBag.WarehouseItemId = warehouseItemId;
        ViewBag.Items = await _context.WarehouseItems
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == scope.CompanyNetworkId && i.IsActive)
          .OrderBy(i => i.Name)
          .Select(i => new { i.Id, i.Name })
          .ToListAsync();
        return View(rows);
    }

    private async Task<WarehouseStocktakeFormViewModel> BuildFormAsync(int companyNetworkId, int? warehouseItemId)
    {
        Dictionary<int, decimal> onHand = await _warehouseStock.GetOnHandByItemIdAsync(companyNetworkId);
        List<WarehouseItem> items = await _context.WarehouseItems
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == companyNetworkId && i.IsActive)
          .Where(i => !warehouseItemId.HasValue || i.Id == warehouseItemId.Value)
          .OrderBy(i => i.Name)
          .ToListAsync();

        return new WarehouseStocktakeFormViewModel
        {
            WarehouseItemId = warehouseItemId,
            Rows = items.Select(i => new WarehouseStocktakeRowViewModel
            {
                WarehouseItemId = i.Id,
                Name = i.Name,
                ModelNumber = i.ModelNumber,
                SystemQuantity = onHand.GetValueOrDefault(i.Id, 0m)
            }).ToList()
        };
    }
}
