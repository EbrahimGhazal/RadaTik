using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services.MaintenancePricing;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class MaintenancePricingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMaintenancePricingService _maintenancePricingService;

    public MaintenancePricingController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IMaintenancePricingService maintenancePricingService)
    {
        _db = db;
        _userManager = userManager;
        _maintenancePricingService = maintenancePricingService;
    }

    [RequirePermission("MaintenancePricing.View")]
    public async Task<IActionResult> Index(string networkScope = "main")
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        MaintenancePricingPageViewModel? pageModel = await _maintenancePricingService.BuildPageModelAsync(selectedNetworkId.Value, networkScope);
        if (pageModel == null)
        {
            TempData["Error"] = AppMessages.CurrentNetworkNotFound;
            return RedirectToAction("Index", "Network");
        }
        return View(pageModel);
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        MaintenancePricingOperationResult saveResult = await _maintenancePricingService.SaveSingleAsync(
            selectedNetworkId.Value,
            networkScope,
            maintenanceType,
            amountSyp,
            isActive,
            user.Id);
        if (!saveResult.Success)
        {
            TempData["Error"] = saveResult.ErrorMessage;
            return RedirectToAction(nameof(Index), new { networkScope });
        }
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(Index), new { networkScope });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MaintenancePricing.Manage")]
    public async Task<IActionResult> SaveAll(MaintenancePricingBulkSaveInput input)
    {
        List<MaintenancePricingBulkSaveRowInput> rows = input?.Rows ?? [];
        string networkScope = _maintenancePricingService.NormalizeScope(input?.NetworkScope);
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        MaintenancePricingOperationResult saveResult = await _maintenancePricingService.SaveRowsAsync(
            selectedNetworkId.Value,
            networkScope,
            rows,
            user.Id);
        if (!saveResult.Success)
        {
            TempData["Error"] = saveResult.ErrorMessage;
            return RedirectToAction(nameof(Index), new { networkScope });
        }
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(Index), new { networkScope });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("MaintenancePricing.Manage")]
    public async Task<IActionResult> CopyFromOtherScope(string networkScope = "main")
    {
        string scope = _maintenancePricingService.NormalizeScope(networkScope);
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        MaintenancePricingOperationResult copyResult = await _maintenancePricingService.CopyFromOtherScopeAsync(
            selectedNetworkId.Value,
            scope,
            user.Id);
        if (!copyResult.Success)
        {
            TempData["Error"] = copyResult.ErrorMessage;
            return RedirectToAction(nameof(Index), new { networkScope = scope });
        }
        TempData["Success"] = $"تم نسخ {copyResult.AffectedCount} طريقة حل من النطاق الآخر بنجاح.";
        return RedirectToAction(nameof(Index), new { networkScope = scope });
    }
}
