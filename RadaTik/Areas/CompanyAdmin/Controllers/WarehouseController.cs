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
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Warehouse)]
public class WarehouseController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWarehouseStockService _warehouseStock;

    public WarehouseController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IWarehouseStockService warehouseStock)
    {
        _context = context;
        _userManager = userManager;
        _warehouseStock = warehouseStock;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? movementType, string? prefillNotes, int? warehouseItemId)
    {
        ViewData["Title"] = "المستودع";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.BusinessModuleTitle = "المستودع";
        ViewBag.BusinessModuleHint = "الرصيد يُحسب بالقطعة. الشراء بالكرتونة من «شراء مواد». الصنف الجديد يُنشأ تلقائياً من فاتورة الشراء — لا حاجة لإضافته هنا أولاً.";
        ViewBag.WarehouseHintsMode = "warehouse";

        List<WarehouseItemRowViewModel> rows = await LoadItemRowsAsync(scope.CompanyNetworkId);
        ViewBag.Items = rows;
        ViewBag.LowStockCount = rows.Count(r => r.IsActive && r.OnHand <= 0m);

        List<WarehouseMovement> recentMovements = await _context.WarehouseMovements
          .AsNoTracking()
          .Include(m => m.WarehouseItem)
          .Include(m => m.CreatedByUser)
          .Where(m => m.CompanyNetworkId == scope.CompanyNetworkId)
          .OrderByDescending(m => m.MovementDate)
          .ThenByDescending(m => m.Id)
          .Take(40)
          .ToListAsync();
        ViewBag.RecentMovements = recentMovements;
        if (movementType is 1 or 2 or 3)
        {
            ViewBag.PrefillMovementType = movementType.Value;
        }

        if (!string.IsNullOrWhiteSpace(prefillNotes))
        {
            ViewBag.PrefillNotes = prefillNotes.Trim();
        }

        if (warehouseItemId is > 0 && rows.Any(r => r.Id == warehouseItemId.Value))
        {
            ViewBag.PrefillWarehouseItemId = warehouseItemId.Value;
        }

        ViewBag.WarehouseNavCurrent = "warehouse";
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateItemPrices(
      int id,
      string? modelNumber,
      decimal? purchasePrice,
      decimal? wholesalePrice,
      decimal? retailPrice)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        WarehouseItem? item = await _context.WarehouseItems
          .FirstOrDefaultAsync(i => i.Id == id && i.CompanyNetworkId == scope.CompanyNetworkId);
        if (item == null)
        {
            TempData["Error"] = "الصنف غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        item.ModelNumber = string.IsNullOrWhiteSpace(modelNumber) ? null : modelNumber.Trim();
        item.PurchasePrice = purchasePrice is > 0m ? purchasePrice : null;
        item.WholesalePrice = wholesalePrice is > 0m ? wholesalePrice : null;
        item.RetailPrice = retailPrice is > 0m ? retailPrice : null;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تحديث أسعار الصنف.";
        return RedirectToAction(nameof(ItemHistory), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(string name, string? unit, string? sku, string? modelNumber, string? notes)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "اسم الصنف مطلوب.";
            return RedirectToAction(nameof(Index));
        }

        _context.WarehouseItems.Add(new WarehouseItem
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            Name = name,
            Unit = string.IsNullOrWhiteSpace(unit) ? "قطعة" : unit.Trim(),
            Sku = string.IsNullOrWhiteSpace(sku) ? null : sku.Trim(),
            ModelNumber = string.IsNullOrWhiteSpace(modelNumber) ? null : modelNumber.Trim(),
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim()
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = "تمت إضافة الصنف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMovement(
      int warehouseItemId,
      WarehouseMovementType movementType,
      decimal quantity,
      DateTime? movementDate,
      string? notes)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        WarehouseItem? item = await _context.WarehouseItems
          .FirstOrDefaultAsync(i => i.Id == warehouseItemId && i.CompanyNetworkId == scope.CompanyNetworkId);
        if (item == null)
        {
            TempData["Error"] = "الصنف غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        if (movementType is WarehouseMovementType.In or WarehouseMovementType.Out)
        {
            if (quantity <= 0m)
            {
                TempData["Error"] = "الكمية يجب أن تكون أكبر من صفر.";
                return RedirectToAction(nameof(Index));
            }
        }
        else if (quantity == 0m)
        {
            TempData["Error"] = "أدخل كمية تصحيح مختلفة عن صفر (موجبة للزيادة، سالبة للنقص).";
            return RedirectToAction(nameof(Index));
        }

        if (movementType == WarehouseMovementType.Out)
        {
            List<WarehouseMovement> existing = await _context.WarehouseMovements
              .AsNoTracking()
              .Where(m => m.WarehouseItemId == warehouseItemId)
              .ToListAsync();
            decimal onHand = _warehouseStock.ComputeOnHand(existing);
            if (onHand < quantity)
            {
                TempData["Error"] = $"الكمية المتوفرة ({WarehouseMaterialQuantityHelper.FormatQuantity(onHand)}) أقل من المطلوب إخراجه.";
                return RedirectToAction(nameof(Index));
            }
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        _context.WarehouseMovements.Add(new WarehouseMovement
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            WarehouseItemId = warehouseItemId,
            MovementType = movementType,
            Quantity = quantity,
            MovementDate = movementDate?.Date ?? DateTime.Today,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            CreatedByUserId = user?.Id
        });
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تسجيل الحركة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleItem(int id, bool isActive)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        WarehouseItem? item = await _context.WarehouseItems
          .FirstOrDefaultAsync(i => i.Id == id && i.CompanyNetworkId == scope.CompanyNetworkId);
        if (item == null)
        {
            TempData["Error"] = "الصنف غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        item.IsActive = isActive;
        await _context.SaveChangesAsync();
        TempData["Success"] = isActive ? "تم تفعيل الصنف." : "تم إيقاف الصنف.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> ItemHistory(int id)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        WarehouseItem? item = await _context.WarehouseItems
          .AsNoTracking()
          .FirstOrDefaultAsync(i => i.Id == id && i.CompanyNetworkId == scope.CompanyNetworkId);
        if (item == null)
        {
            TempData["Error"] = "الصنف غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        List<WarehouseMovement> movements = await _context.WarehouseMovements
          .AsNoTracking()
          .Include(m => m.CreatedByUser)
          .Where(m => m.WarehouseItemId == id)
          .OrderByDescending(m => m.MovementDate)
          .ThenByDescending(m => m.Id)
          .ToListAsync();

        ViewData["Title"] = $"حركات: {item.Name}";
        ViewBag.Item = item;
        ViewBag.OnHand = _warehouseStock.ComputeOnHand(movements);
        ViewBag.Movements = movements;
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.WarehouseNavCurrent = "warehouse";
        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Print()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        List<WarehouseItemRowViewModel> rows = await LoadItemRowsAsync(scope.CompanyNetworkId);
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.PrintTitle = "جرد المستودع";
        ViewBag.Items = rows;
        ViewBag.PrintDate = DateTime.Today.ToString("yyyy-MM-dd");
        return View("Print");
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        List<WarehouseItemRowViewModel> rows = await LoadItemRowsAsync(scope.CompanyNetworkId);
        string fileName = CompanyBusinessExcelHelper.SanitizeFileName($"مستودع_{scope.CompanyNetworkName}_{DateTime.Today:yyyyMMdd}.xlsx");

        byte[] bytes = CompanyBusinessExcelHelper.BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = $"جرد المستودع — {scope.CompanyNetworkName} — {DateTime.Today:yyyy-MM-dd}";
            int row = 3;
            ws.Cell(row, 1).Value = "الصنف";
            ws.Cell(row, 2).Value = "SKU";
            ws.Cell(row, 3).Value = "الكمية";
            ws.Cell(row, 4).Value = "الوحدة";
            ws.Cell(row, 5).Value = "الحالة";
            ws.Row(row).Style.Font.Bold = true;
            row++;
            foreach (WarehouseItemRowViewModel i in rows)
            {
                ws.Cell(row, 1).Value = i.Name;
                ws.Cell(row, 2).Value = i.Sku ?? "";
                ws.Cell(row, 3).Value = i.OnHand;
                ws.Cell(row, 4).Value = i.Unit ?? "قطعة";
                ws.Cell(row, 5).Value = i.IsActive ? "نشط" : "موقوف";
                row++;
            }
        });

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
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
