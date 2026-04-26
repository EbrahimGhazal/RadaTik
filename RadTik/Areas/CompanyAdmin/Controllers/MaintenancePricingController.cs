using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Areas.CompanyAdmin.ViewModels;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class MaintenancePricingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public MaintenancePricingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    [RequirePermission("MaintenancePricing.View")]
    public async Task<IActionResult> Index(string networkScope = "main")
    {
        var user = await _userManager.GetUserAsync(User);
        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction("Index", "Network");
        }

        var selectedNetwork = await _db.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة الحالية.";
            return RedirectToAction("Index", "Network");
        }

        var companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var scope = NormalizeScope(networkScope);
        if (scope == "current" && selectedNetwork.ParentNetworkId == null)
        {
            scope = "main";
        }
        var targetNetworkId = scope == "current" ? selectedNetwork.Id : companyNetworkId;

        var targetNetworkName = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == targetNetworkId)
            .Select(n => n.Name)
            .FirstOrDefaultAsync() ?? $"#{targetNetworkId}";

        var prices = await _db.NetworkMaintenancePrices
            .AsNoTracking()
            .Where(x => x.NetworkId == targetNetworkId)
            .ToListAsync();

        var rows = MaintenanceCatalog.SolutionTypes
            .Select(t =>
            {
                var p = prices.FirstOrDefault(x => x.MaintenanceType == t);
                return new MaintenancePricingRowViewModel
                {
                    Type = t,
                    SolutionName = MaintenanceCatalog.GetDisplayName(t),
                    AmountSyp = p?.AmountSYP ?? 0m,
                    IsActive = p?.IsActive ?? false
                };
            })
            .ToList();

        return View(new MaintenancePricingPageViewModel
        {
            NetworkId = targetNetworkId,
            NetworkScope = scope,
            EffectiveNetworkName = targetNetworkName,
            CanUseCurrentNetworkScope = selectedNetwork.ParentNetworkId.HasValue,
            Rows = rows
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MaintenancePricing.Manage")]
    public async Task<IActionResult> Save(MaintenanceType maintenanceType, decimal amountSyp, bool isActive, string networkScope = "main")
    {
        if (!MaintenanceCatalog.IsSolutionType(maintenanceType))
        {
            TempData["Error"] = "التسعير متاح فقط لطرق الحل.";
            return RedirectToAction(nameof(Index), new { networkScope });
        }

        if (amountSyp < 0m)
        {
            TempData["Error"] = "قيمة السعر يجب أن تكون صفر أو أكبر.";
            return RedirectToAction(nameof(Index), new { networkScope });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction("Index", "Network");
        }

        var targetNetworkId = await ResolveTargetNetworkIdAsync(selectedNetworkId.Value, networkScope);
        var existing = await _db.NetworkMaintenancePrices
            .FirstOrDefaultAsync(x => x.NetworkId == targetNetworkId && x.MaintenanceType == maintenanceType);

        if (existing == null)
        {
            existing = new NetworkMaintenancePrice
            {
                NetworkId = targetNetworkId,
                MaintenanceType = maintenanceType,
                AmountSYP = amountSyp,
                IsActive = isActive,
                UpdatedByUserId = user.Id,
                UpdatedAt = DateTime.Now
            };
            _db.NetworkMaintenancePrices.Add(existing);
        }
        else
        {
            existing.AmountSYP = amountSyp;
            existing.IsActive = isActive;
            existing.UpdatedByUserId = user.Id;
            existing.UpdatedAt = DateTime.Now;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ تسعير الصيانة.";
        return RedirectToAction(nameof(Index), new { networkScope });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MaintenancePricing.Manage")]
    public async Task<IActionResult> SaveAll(MaintenancePricingBulkSaveInput input)
    {
        var rows = input?.Rows ?? [];
        var networkScope = NormalizeScope(input?.NetworkScope);
        if (rows.Count == 0)
        {
            TempData["Error"] = "لا توجد بيانات للحفظ.";
            return RedirectToAction(nameof(Index), new { networkScope });
        }

        if (rows.Any(r => r.AmountSyp < 0m))
        {
            TempData["Error"] = "جميع الأسعار يجب أن تكون صفر أو أكبر.";
            return RedirectToAction(nameof(Index), new { networkScope });
        }

        if (rows.Any(r => !MaintenanceCatalog.IsSolutionType(r.Type)))
        {
            TempData["Error"] = "لا يمكن حفظ تسعير لأنواع أعطال. التسعير يطبق فقط على طرق الحل.";
            return RedirectToAction(nameof(Index), new { networkScope });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction("Index", "Network");
        }

        var targetNetworkId = await ResolveTargetNetworkIdAsync(selectedNetworkId.Value, networkScope);
        var targetTypes = rows.Select(r => r.Type).Distinct().ToList();

        var existingByType = await _db.NetworkMaintenancePrices
            .Where(x => x.NetworkId == targetNetworkId && targetTypes.Contains(x.MaintenanceType))
            .ToDictionaryAsync(x => x.MaintenanceType);

        var now = DateTime.Now;
        foreach (var row in rows)
        {
            if (!existingByType.TryGetValue(row.Type, out var existing))
            {
                existing = new NetworkMaintenancePrice
                {
                    NetworkId = targetNetworkId,
                    MaintenanceType = row.Type
                };
                _db.NetworkMaintenancePrices.Add(existing);
                existingByType[row.Type] = existing;
            }

            existing.AmountSYP = row.AmountSyp;
            existing.IsActive = row.IsActive;
            existing.UpdatedByUserId = user.Id;
            existing.UpdatedAt = now;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ جميع أسعار الصيانة بنجاح.";
        return RedirectToAction(nameof(Index), new { networkScope });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MaintenancePricing.Manage")]
    public async Task<IActionResult> CopyFromOtherScope(string networkScope = "main")
    {
        var scope = NormalizeScope(networkScope);
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction("Index", "Network");
        }

        var selectedNetwork = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == selectedNetworkId.Value)
            .Select(n => new { n.Id, n.ParentNetworkId })
            .FirstOrDefaultAsync();
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة الحالية.";
            return RedirectToAction(nameof(Index), new { networkScope = scope });
        }

        var mainNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var currentNetworkId = selectedNetwork.Id;
        var targetNetworkId = scope == "current" && selectedNetwork.ParentNetworkId.HasValue
            ? currentNetworkId
            : mainNetworkId;
        var sourceNetworkId = targetNetworkId == mainNetworkId ? currentNetworkId : mainNetworkId;
        if (sourceNetworkId == targetNetworkId)
        {
            TempData["Error"] = "لا يوجد نطاق آخر للنسخ منه ضمن الشبكة الحالية.";
            return RedirectToAction(nameof(Index), new { networkScope = scope });
        }

        var targetTypes = MaintenanceCatalog.SolutionTypes.ToList();
        var sourceByType = await _db.NetworkMaintenancePrices
            .AsNoTracking()
            .Where(x => x.NetworkId == sourceNetworkId && targetTypes.Contains(x.MaintenanceType))
            .GroupBy(x => x.MaintenanceType)
            .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(x => x.Id).First());

        if (sourceByType.Count == 0)
        {
            TempData["Error"] = "لا توجد أسعار في النطاق المصدر لنسخها.";
            return RedirectToAction(nameof(Index), new { networkScope = scope });
        }

        var targetExisting = await _db.NetworkMaintenancePrices
            .Where(x => x.NetworkId == targetNetworkId && targetTypes.Contains(x.MaintenanceType))
            .ToDictionaryAsync(x => x.MaintenanceType);

        var now = DateTime.Now;
        var copiedCount = 0;
        foreach (var type in targetTypes)
        {
            if (!sourceByType.TryGetValue(type, out var source))
            {
                continue;
            }

            if (!targetExisting.TryGetValue(type, out var target))
            {
                target = new NetworkMaintenancePrice
                {
                    NetworkId = targetNetworkId,
                    MaintenanceType = type
                };
                _db.NetworkMaintenancePrices.Add(target);
                targetExisting[type] = target;
            }

            target.AmountSYP = source.AmountSYP;
            target.IsActive = source.IsActive;
            target.UpdatedByUserId = user.Id;
            target.UpdatedAt = now;
            copiedCount++;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"تم نسخ {copiedCount} طريقة حل من النطاق الآخر بنجاح.";
        return RedirectToAction(nameof(Index), new { networkScope = scope });
    }

    private async Task<int> ResolveTargetNetworkIdAsync(int selectedNetworkId, string networkScope)
    {
        var net = await _db.Networks.AsNoTracking()
            .Where(n => n.Id == selectedNetworkId)
            .Select(n => new { n.Id, n.ParentNetworkId })
            .FirstAsync();

        var scope = NormalizeScope(networkScope);
        if (scope == "current" && net.ParentNetworkId.HasValue)
        {
            return net.Id;
        }

        return net.ParentNetworkId ?? net.Id;
    }

    private static string NormalizeScope(string? networkScope)
        => string.Equals(networkScope, "current", StringComparison.OrdinalIgnoreCase) ? "current" : "main";
}
