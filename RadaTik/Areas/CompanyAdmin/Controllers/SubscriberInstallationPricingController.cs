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

using global::RadaTik.Services;



namespace RadaTik.Areas.CompanyAdmin.Controllers;



[Area("CompanyAdmin")]

[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]

public class SubscriberInstallationPricingController : Controller

{

    private readonly ApplicationDbContext _db;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly ISubscriberInstallationInvoiceService _pricingService;

    private readonly SubscriberInstallationWarehouseLinkService _warehouseLinkService;



    public SubscriberInstallationPricingController(

        ApplicationDbContext db,

        UserManager<ApplicationUser> userManager,

        ISubscriberInstallationInvoiceService pricingService,

        SubscriberInstallationWarehouseLinkService warehouseLinkService)

    {

        _db = db;

        _userManager = userManager;

        _pricingService = pricingService;

        _warehouseLinkService = warehouseLinkService;

    }



    public async Task<IActionResult> Index()

    {

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);

        if (!selectedNetworkId.HasValue)

        {

            TempData["Error"] = AppMessages.SelectNetworkFirst;

            return RedirectToAction("Index", "Network");

        }



        return View(await BuildPageModelAsync(selectedNetworkId.Value));

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> AutoLinkWarehouse()

    {

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);

        if (!selectedNetworkId.HasValue)

        {

            TempData["Error"] = AppMessages.SelectNetworkFirst;

            return RedirectToAction("Index", "Network");

        }



        await _pricingService.GetOrCreateMaterialPricesAsync(selectedNetworkId.Value);

        AutoLinkWarehouseResult result = await _warehouseLinkService.AutoLinkAsync(selectedNetworkId.Value);



        if (result.LinkedCount > 0)

        {

            TempData["Success"] = $"تم ربط {result.LinkedCount} موديل/صنف من المستودع تلقائياً.";

        }

        else

        {

            TempData["Info"] = "لم يُعثر على أصناف مطابقة جديدة. اختر الموديلات يدوياً.";

        }



        if (result.UnmatchedMaterialNames.Count > 0)

        {

            TempData["Warning"] = "مواد لم يُربط لها أي موديل: " + string.Join("، ", result.UnmatchedMaterialNames);

        }



        return RedirectToAction(nameof(Index));

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SaveAll(SubscriberInstallationPricingSaveInput input)

    {

        List<SubscriberInstallationPricingSaveRowInput> rows = input?.Rows ?? [];

        if (rows.Count == 0)

        {

            TempData["Error"] = "لا توجد بيانات للحفظ.";

            return RedirectToAction(nameof(Index));

        }



        if (rows.Any(r => r.UnitPrice < 0m))

        {

            TempData["Error"] = "جميع الأسعار يجب أن تكون صفر أو أكبر.";

            return RedirectToAction(nameof(Index));

        }



        foreach (SubscriberInstallationPricingSaveRowInput row in rows)

        {

            row.WarehouseItemIds = row.WarehouseItemIds.Where(id => id > 0).Distinct().ToList();

            if (row.DefaultWarehouseItemId is > 0 && !row.WarehouseItemIds.Contains(row.DefaultWarehouseItemId.Value))

            {

                row.WarehouseItemIds.Insert(0, row.DefaultWarehouseItemId.Value);

            }

        }



        List<(int WarehouseItemId, string MaterialKey)> activeLinks = rows

            .Where(r => SubscriberInstallationWarehouseLinkService.IsStockMaterialKey(r.MaterialKey)

                        && r.IsActive)

            .SelectMany(r => r.WarehouseItemIds.Select(id => (id, r.MaterialKey)))

            .ToList();



        if (activeLinks.GroupBy(x => x.WarehouseItemId).Any(g => g.Select(x => x.MaterialKey).Distinct().Count() > 1))

        {

            TempData["Error"] = "لا يمكن ربط نفس صنف/موديل المستودع بأكثر من مادة.";

            return RedirectToAction(nameof(Index));

        }



        if (rows.Any(r => r.IsActive

                          && SubscriberInstallationWarehouseLinkService.IsStockMaterialKey(r.MaterialKey)

                          && r.WarehouseItemIds.Count == 0))

        {

            TempData["Error"] = "يجب ربط كل مادة مخزنية مفعّلة بموديل واحد على الأقل (أو إلغاء تفعيلها).";

            return RedirectToAction(nameof(Index));

        }



        ApplicationUser? user = await _userManager.GetUserAsync(User);

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);

        if (!selectedNetworkId.HasValue)

        {

            TempData["Error"] = AppMessages.SelectNetworkFirst;

            return RedirectToAction("Index", "Network");

        }



        int companyNetworkId = await ResolveCompanyNetworkIdAsync(selectedNetworkId.Value);

        HashSet<int> validWarehouseIds = await _db.WarehouseItems

            .AsNoTracking()

            .Where(w => w.CompanyNetworkId == companyNetworkId && w.IsActive)

            .Select(w => w.Id)

            .ToHashSetAsync();



        foreach (SubscriberInstallationPricingSaveRowInput row in rows)

        {

            if (!SubscriberInstallationWarehouseLinkService.IsStockMaterialKey(row.MaterialKey))

            {

                row.WarehouseItemIds = [];

                row.DefaultWarehouseItemId = null;

                continue;

            }



            if (row.WarehouseItemIds.Any(id => !validWarehouseIds.Contains(id)))

            {

                TempData["Error"] = "أحد أصناف المستودع غير صالح أو غير نشط.";

                return RedirectToAction(nameof(Index));

            }

        }



        await _pricingService.SaveMaterialPricesWithModelsAsync(

            selectedNetworkId.Value,

            rows.Select(r => new MaterialPriceSaveRow

            {

                MaterialKey = r.MaterialKey,

                UnitPrice = r.UnitPrice,

                IsActive = r.IsActive,

                DefaultWarehouseItemId = r.DefaultWarehouseItemId is > 0

                    ? r.DefaultWarehouseItemId

                    : r.WarehouseItemIds.FirstOrDefault(),

                WarehouseItemIds = r.WarehouseItemIds

            }));

        await _db.SaveChangesAsync();



        TempData["Success"] = AppMessages.OperationSuccess;

        return RedirectToAction(nameof(Index));

    }



    private async Task<SubscriberInstallationPricingPageViewModel> BuildPageModelAsync(int networkId)

    {

        string networkName = await _db.Networks

            .AsNoTracking()

            .Where(n => n.Id == networkId)

            .Select(n => n.Name)

            .FirstOrDefaultAsync() ?? $"شركة {networkId}";



        IReadOnlyList<SubscriberInstallationMaterialPrice> materialRows =

            await _pricingService.GetOrCreateMaterialPricesAsync(networkId);



        List<SubscriberInstallationMaterialPrice> materialsWithLinks = await _db.SubscriberInstallationMaterialPrices

            .AsNoTracking()

            .Include(m => m.WarehouseLinks)

            .Where(m => m.NetworkId == networkId)

            .ToListAsync();



        Dictionary<string, SubscriberInstallationMaterialPrice> materialByKey =

            materialsWithLinks.ToDictionary(m => m.MaterialKey, StringComparer.OrdinalIgnoreCase);



        List<WarehouseItemOptionViewModel> warehouseOptions = await LoadWarehouseItemOptionsAsync(networkId);

        Dictionary<int, decimal> onHandMap = warehouseOptions.ToDictionary(w => w.Id, w => w.OnHand);

        PricingWarehouseReadiness readiness = await _warehouseLinkService.GetReadinessAsync(networkId);



        return new SubscriberInstallationPricingPageViewModel

        {

            NetworkId = networkId,

            NetworkName = networkName,

            UnlinkedStockLineCount = readiness.UnlinkedStockLineCount,

            IsReadyForWarehouseFinalize = readiness.IsReadyForWarehouseFinalize,

            WarehouseItems = warehouseOptions,

            Rows = materialRows.Select(r =>

            {

                materialByKey.TryGetValue(r.MaterialKey, out SubscriberInstallationMaterialPrice? full);

                List<int> linkedIds = full?.WarehouseLinks.Select(l => l.WarehouseItemId).ToList() ?? [];

                int? defaultId = full != null

                    ? SubscriberInstallationWarehouseLinkService.ResolveDefaultWarehouseItemId(full)

                    : r.WarehouseItemId;



                return new SubscriberInstallationPricingRowViewModel

                {

                    MaterialKey = r.MaterialKey,

                    MaterialName = r.MaterialName,

                    UnitPrice = r.UnitPrice,

                    IsActive = r.IsActive,

                    DefaultWarehouseItemId = defaultId,

                    LinkedWarehouseItemIds = linkedIds.Count > 0

                        ? linkedIds

                        : r.WarehouseItemId is > 0 ? [r.WarehouseItemId.Value] : [],

                    RequiresWarehouse = SubscriberInstallationWarehouseLinkService.IsStockMaterialKey(r.MaterialKey),

                    WarehouseOnHand = defaultId.HasValue && onHandMap.TryGetValue(defaultId.Value, out decimal qty)

                        ? qty

                        : null

                };

            }).ToList()

        };

    }



    private async Task<List<WarehouseItemOptionViewModel>> LoadWarehouseItemOptionsAsync(int networkId)

    {

        int companyId = await ResolveCompanyNetworkIdAsync(networkId);

        List<Models.Business.WarehouseItem> items = await _db.WarehouseItems

            .AsNoTracking()

            .Where(w => w.CompanyNetworkId == companyId && w.IsActive)

            .OrderBy(w => w.Name)

            .ThenBy(w => w.ModelNumber)

            .ToListAsync();



        IReadOnlyDictionary<int, decimal> onHand =

            await _warehouseLinkService.GetOnHandByWarehouseItemIdAsync(networkId);



        return items.Select(w => new WarehouseItemOptionViewModel

        {

            Id = w.Id,

            Name = w.Name,

            ModelNumber = w.ModelNumber,

            Sku = w.Sku,

            OnHand = onHand.TryGetValue(w.Id, out decimal qty) ? qty : 0m

        }).ToList();

    }



    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId)

    {

        Network? net = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId);

        return net?.ParentNetworkId ?? networkId;

    }

}

