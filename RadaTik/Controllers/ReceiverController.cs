using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Helpers;
using RadaTik.Security;
using RadaTik.Services.PricingPreview;
using RadaTik.Services;
using RadaTik.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace RadaTik.Controllers
{
    // CompanyEmployee هو الدور الجديد للموظف التابع للشركة، و EmployeeLegacy للتوافق.
    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Receivers)]
    public partial class ReceiverController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ICreatePricingPreviewService _pricingPreviewService;
        private readonly ILineOfSightAnalysisService _lineOfSightAnalysisService;

        public ReceiverController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ICreatePricingPreviewService pricingPreviewService,
            ILineOfSightAnalysisService lineOfSightAnalysisService)
        {
            _context = context;
            _userManager = userManager;
            _usageChargeService = usageChargeService;
            _pricingPreviewService = pricingPreviewService;
            _lineOfSightAnalysisService = lineOfSightAnalysisService;
        }

        // GET: Receiver
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> Index()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // جلب المستقبلات للشبكة الحالية
            List<Receiver> receivers = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .Include(r => r.Clients)
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            ViewBag.PendingReceiverIds = await GetPendingReceiverIdsAsync(networkId.Value);

            // جلب المرسلات (القطاعات) مع المستقبلات الخاصة بها للشبكة الحالية من أجل الخريطة
            List<Sector> sectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.Receivers)
                .ToListAsync();

            IEnumerable<ReceiverMapSectorSummaryJson> mapData = sectors.Select(s => new ReceiverMapSectorSummaryJson(
                s.Id,
                s.Name,
                s.Latitude,
                s.Longitude,
                s.Direction,
                s.CoverageAngle,
                s.CoverageRange,
                s.Receivers.Select(r => new ReceiverMapPointJson(r.Id, r.Name, r.Latitude, r.Longitude, r.IPAddress))));

            ViewBag.MapDataJson = JsonSerializer.Serialize(mapData);

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = networkId.Value;
            return View(receivers);
        }

        // GET: Receiver/Details/5
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Receiver? receiver = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .Include(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (receiver == null)
            {
                return NotFound();
            }

            HashSet<int> pendingReceiverIds = await GetPendingReceiverIdsAsync(networkId.Value);
            ViewBag.IsPendingReceiverApproval = pendingReceiverIds.Contains(receiver.Id);

            return View(receiver);
        }

        // GET: Receiver/Create
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Receivers.Create")]
        public async Task<IActionResult> Create(string? returnUrl)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            ViewBag.WizardReturnUrl = IsSafeReturnUrl(returnUrl) ? returnUrl : null;
            await PopulateReceiverCreateViewDataAsync(user, networkId.Value, selectedSectorId: null);
            return View(new Receiver());
        }

        // POST: Receiver/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Receivers.Create")]
        public async Task<IActionResult> Create(Receiver receiver, string? returnUrl)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            string? safeReturnUrl = IsSafeReturnUrl(returnUrl) ? returnUrl : null;
            ViewBag.WizardReturnUrl = safeReturnUrl;

            IList<string> userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            bool isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
            IReadOnlyCollection<int> companyScope = await ResolveReceiverSectorScopeAsync(networkId.Value, isCompanyAdmin);

            if (ModelState.IsValid)
            {
                // التحقق من أن القطاع يتبع نفس الشبكة
                Sector? sector = await _context.Sectors
                    .FirstOrDefaultAsync(s =>
                        s.Id == receiver.SectorId &&
                        s.NetworkId.HasValue &&
                        companyScope.Contains(s.NetworkId.Value) &&
                        s.IsActive);
                if (sector == null)
                {
                    ModelState.AddModelError("SectorId", "المرسل المحدد غير متاح (قد يكون غير معتمد بعد).");
                    await PopulateReceiverCreateViewDataAsync(user, networkId.Value, receiver.SectorId);
                    return View(receiver);
                }

                // قناع الشبكة يطابق قناع القطاع دائماً.
                if (!string.IsNullOrWhiteSpace(sector.NetworkMask))
                {
                    receiver.NetworkMask = sector.NetworkMask.Trim();
                }

                // اربط المستقبل بنفس شبكة القطاع المختار لتجنب عدم التطابق ضمن نطاق الشركة.
                receiver.NetworkId = sector.NetworkId;

                bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy);
                if (isEmployee && user != null)
                {
                    receiver.IsActive = false;
                    _context.Add(receiver);
                    await _context.SaveChangesAsync();

                    string requestNotes = EmployeeApprovalRequestHelper.BuildReceiverCreate(receiver.Id);
                    Network? pricingNetwork = await _context.Networks
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                    int pricingCompanyNetworkId = pricingNetwork?.ParentNetworkId ?? networkId.Value;
                    UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                        pricingCompanyNetworkId,
                        PricingChargeUnit.PerReceiver,
                        1);
                    await CreateEmployeeApprovalRequestAsync(
                        networkId.Value,
                        user.Id,
                        FeatureKeys.Receivers,
                        requestNotes,
                        estimate.RequiredAmountSyp);

                    TempData["Info"] = "تم تسجيل إضافة المستقبل كطلب موافقة. سيُفعَّل بعد اعتماد مدير الشركة.";
                    return RedirectAfterReceiverCreate(safeReturnUrl, receiver.Id, nameof(Index));
                }

                _context.Add(receiver);
                await _context.SaveChangesAsync();

                Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
                await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, user!.Id, PricingChargeUnit.PerReceiver);

                TempData["Success"] = AppMessages.OperationSuccess;
                return RedirectAfterReceiverCreate(safeReturnUrl, receiver.Id, nameof(Index));
            }
            await PopulateReceiverCreateViewDataAsync(user, networkId.Value, receiver.SectorId);
            return View(receiver);
        }

        private IActionResult RedirectAfterReceiverCreate(string? returnUrl, int receiverId, string fallbackAction)
        {
            if (!string.IsNullOrWhiteSpace(returnUrl))
            {
                string separator = returnUrl.Contains('?') ? "&" : "?";
                return Redirect($"{returnUrl}{separator}receiverId={receiverId}");
            }

            return RedirectToAction(fallbackAction);
        }

        private static bool IsSafeReturnUrl(string? returnUrl) =>
            !string.IsNullOrWhiteSpace(returnUrl)
            && Uri.TryCreate(returnUrl, UriKind.Relative, out Uri? uri)
            && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.Contains("://", StringComparison.Ordinal);

        /// <summary>
        /// بيانات القائمة المنسدلة + JSON الخريطة + IP التالي — مطلوبة في GET وفي POST عند إعادة عرض النموذج.
        /// </summary>
        private async Task PopulateReceiverCreateViewDataAsync(ApplicationUser? user, int networkId, int? selectedSectorId)
        {
            IList<string> userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            bool isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
            IReadOnlyCollection<int> sectorScope = await ResolveReceiverSectorScopeAsync(networkId, isCompanyAdmin);

            List<Sector> sectors = await _context.Sectors
                .Where(s => s.NetworkId.HasValue && sectorScope.Contains(s.NetworkId.Value) && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
            ViewData["SectorId"] = new SelectList(sectors, "Id", "Name", selectedSectorId);
            ViewBag.ReceiverCreateSectors = sectors
                .Select(s => new ReceiverCreateSectorOption(s.Id, s.Name, s.MikroTikServerId, s.NetworkMask, s.IPAddress))
                .ToList();

            HashSet<int> serverIds = sectors.Select(s => s.MikroTikServerId).ToHashSet();
            ViewBag.MikroTikServersForFilter = await _context.MikroTikServers
                .AsNoTracking()
                .Where(s => serverIds.Contains(s.Id))
                .OrderBy(s => s.Name)
                .Select(s => new ReceiverCreateServerOption(s.Id, s.Name))
                .ToListAsync();

            List<Sector> sectorsForMap = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId.HasValue && sectorScope.Contains(s.NetworkId.Value) && s.IsActive)
                .Include(s => s.Receivers)
                .ToListAsync();

            if (sectors.Count == 0)
            {
                int pendingSectorCount = await _context.NetworkServiceRequests
                    .AsNoTracking()
                    .Where(r =>
                        sectorScope.Contains(r.NetworkId) &&
                        r.Status == NetworkServiceRequestStatus.Pending &&
                        r.FeatureKey == FeatureKeys.Sectors &&
                        r.Notes != null &&
                        r.Notes.Contains("SECTOR_CREATE_PENDING:"))
                    .CountAsync();
                ViewBag.PendingSectorApprovalsCount = pendingSectorCount;
            }

            IEnumerable<ReceiverCreateMapSectorJson> mapDataForCreate = sectorsForMap.Select(s => new ReceiverCreateMapSectorJson(
                s.Id,
                s.Name,
                s.MikroTikServerId,
                s.Latitude,
                s.Longitude,
                s.Direction,
                s.CoverageAngle,
                s.CoverageRange,
                s.IPAddress,
                s.NetworkMask,
                s.ElevationMeters,
                s.AntennaHeightAglMeters,
                s.Receivers.Select(r => new ReceiverMapPointJson(r.Id, r.Name, r.Latitude, r.Longitude, r.IPAddress))));

            ViewBag.MapDataJson = JsonSerializer.Serialize(mapDataForCreate);

            ViewBag.NextIP = await ResolveNextReceiverIpAsync(networkId, selectedSectorId, sectors);
            ViewBag.DefaultMask = ResolveDefaultMask(selectedSectorId, sectors);
            ViewBag.NetworkId = networkId;
            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            await LoadReceiverCreatePricingNoteAsync(networkId);
        }

        private async Task<string> ResolveNextReceiverIpAsync(int networkId, int? selectedSectorId, List<Sector> sectors)
        {
            if (selectedSectorId is > 0)
            {
                Receiver? lastOnSector = await _context.Receivers
                    .AsNoTracking()
                    .Where(r => r.SectorId == selectedSectorId.Value)
                    .OrderByDescending(r => r.CreatedDate)
                    .ThenByDescending(r => r.Id)
                    .FirstOrDefaultAsync();
                if (lastOnSector != null)
                {
                    return GenerateNextReceiverIP(lastOnSector.IPAddress);
                }

                Sector? sector = sectors.FirstOrDefault(s => s.Id == selectedSectorId.Value);
                if (sector != null && !string.IsNullOrWhiteSpace(sector.IPAddress))
                {
                    string[] parts = sector.IPAddress.Split('.');
                    if (parts.Length == 4)
                    {
                        return $"{parts[0]}.{parts[1]}.{parts[2]}.100";
                    }
                }
            }

            Receiver? lastReceiver = await _context.Receivers
                .AsNoTracking()
                .Where(r => r.NetworkId == networkId)
                .OrderByDescending(r => r.CreatedDate)
                .ThenByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            return lastReceiver != null
                ? GenerateNextReceiverIP(lastReceiver.IPAddress)
                : "192.168.1.100";
        }

        private static string ResolveDefaultMask(int? selectedSectorId, List<Sector> sectors)
        {
            if (selectedSectorId is > 0)
            {
                Sector? sector = sectors.FirstOrDefault(s => s.Id == selectedSectorId.Value);
                if (sector != null && !string.IsNullOrWhiteSpace(sector.NetworkMask))
                {
                    return sector.NetworkMask.Trim();
                }
            }

            return "255.255.255.0";
        }

        private async Task<IReadOnlyCollection<int>> ResolveReceiverSectorScopeAsync(int selectedNetworkId, bool isCompanyAdmin)
        {
            if (!isCompanyAdmin)
            {
                return new[] { selectedNetworkId };
            }

            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;
            return await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);
        }

        private async Task LoadReceiverCreatePricingNoteAsync(int selectedNetworkId)
        {
            try
            {
                Network? selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

                UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerReceiver,
                    1);
                CreatePricingPreviewResult preview = await _pricingPreviewService.BuildAsync(
                    selectedNetworkId,
                    FeatureKeys.Receivers,
                    PricingChargeUnit.PerReceiver,
                    PricingPreviewCounterKeys.Receivers);
                PricingPreviewViewBagMapper.Apply(ViewData, "ReceiverCreate", preview);

                ViewBag.ReceiverCreateChargeHasPricing = estimate.HasCharge;
                ViewBag.ReceiverCreateChargeAmount = estimate.RequiredAmountSyp;
                ViewBag.ReceiverCreateChargeWalletBalance = estimate.WalletBalance > 0m ? estimate.WalletBalance : 0m;
            }
            catch
            {
                ViewBag.ReceiverCreateChargeHasPricing = false;
                ViewBag.ReceiverCreateChargeAmount = 0m;
                ViewBag.ReceiverCreateChargeWalletBalance = 0m;
                PricingPreviewViewBagMapper.Apply(ViewData, "ReceiverCreate", PricingPreviewViewBagMapper.Empty());
            }
        }

        // GET: Receiver/Edit/5
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Receivers.Edit")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Receiver? receiver = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (receiver == null)
            {
                return NotFound();
            }
            List<Sector> sectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value && s.IsActive)
                .ToListAsync();
            ViewData["SectorId"] = new SelectList(sectors, "Id", "Name", receiver.SectorId);
            ViewBag.NetworkId = networkId.Value;
            return View(receiver);
        }

        // POST: Receiver/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Receivers.Edit")]
        public async Task<IActionResult> Edit(int id, Receiver receiver)
        {
            if (id != receiver.Id)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // التحقق من أن المستقبل يتبع الشبكة المحددة
            Receiver? existingReceiver = await _context.Receivers
                .FirstOrDefaultAsync(r => r.Id == id && r.NetworkId == networkId.Value);
            if (existingReceiver == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    IList<string> userRoles = user != null
                        ? await _userManager.GetRolesAsync(user)
                        : Array.Empty<string>();
                    bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy);
                    if (isEmployee && user != null)
                    {
                        ReceiverEditApprovalPayload payload = new ReceiverEditApprovalPayload
                        {
                            Name = receiver.Name,
                            SectorId = receiver.SectorId,
                            IPAddress = receiver.IPAddress,
                            NetworkMask = receiver.NetworkMask,
                            Latitude = receiver.Latitude,
                            Longitude = receiver.Longitude,
                            ElevationMeters = receiver.ElevationMeters,
                            AntennaHeightAglMeters = receiver.AntennaHeightAglMeters,
                            IsActive = receiver.IsActive
                        };
                        string? requestNotes = EmployeeApprovalRequestHelper.BuildReceiverEdit(existingReceiver.Id, payload);
                        if (string.IsNullOrWhiteSpace(requestNotes))
                        {
                            TempData["Error"] = "تعذر حفظ طلب التعديل: حجم البيانات كبير جداً.";
                            return RedirectToAction(nameof(Edit), new { id });
                        }

                        await CreateEmployeeApprovalRequestAsync(
                            networkId.Value,
                            user.Id,
                            FeatureKeys.Receivers,
                            requestNotes,
                            0m);

                        TempData["Info"] = "تم تسجيل تعديل المستقبل كطلب موافقة. سيُطبق بعد اعتماد مدير الشركة.";
                        return RedirectToAction(nameof(Index));
                    }

                    // تحديث الكيان المتتبع الموجود مسبقاً بدلاً من إرفاق نسخة جديدة
                    existingReceiver.Name = receiver.Name;
                    existingReceiver.SectorId = receiver.SectorId;
                    existingReceiver.IPAddress = receiver.IPAddress;
                    existingReceiver.NetworkMask = receiver.NetworkMask;
                    existingReceiver.Latitude = receiver.Latitude;
                    existingReceiver.Longitude = receiver.Longitude;
                    existingReceiver.ElevationMeters = receiver.ElevationMeters;
                    existingReceiver.AntennaHeightAglMeters = receiver.AntennaHeightAglMeters;
                    existingReceiver.IsActive = receiver.IsActive;
                    existingReceiver.NetworkId = networkId.Value; // التأكد من ربطه بالشبكة الصحيحة

                    await _context.SaveChangesAsync();
                    TempData["Success"] = AppMessages.OperationSuccess;
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ReceiverExists(receiver.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            List<Sector> sectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value && s.IsActive)
                .ToListAsync();
            ViewData["SectorId"] = new SelectList(sectors, "Id", "Name", receiver.SectorId);
            ViewBag.NetworkId = networkId.Value;
            return View(receiver);
        }

        // GET: Receiver/Delete/5
        [Authorize(Roles = "NetworkAdministrator")]
        [RequirePermission("Receivers.Delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Receiver? receiver = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .Include(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (receiver == null)
            {
                return NotFound();
            }

            return View(receiver);
        }

        // POST: Receiver/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        [RequirePermission("Receivers.Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Receiver? receiver = await _context.Receivers
                .FirstOrDefaultAsync(r => r.Id == id && r.NetworkId == networkId.Value);
            if (receiver != null)
            {
                _context.Receivers.Remove(receiver);
                await _context.SaveChangesAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
            }
            return RedirectToAction(nameof(Index));
        }

        // API: الحصول على قناع الشبكة للقطاع المحدد
        [HttpGet]
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> GetSectorNetworkMask(int sectorId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                IList<string> userRoles = user != null
                    ? await _userManager.GetRolesAsync(user)
                    : Array.Empty<string>();
                bool isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
                IReadOnlyCollection<int> sectorScope = await ResolveReceiverSectorScopeAsync(networkId.Value, isCompanyAdmin);

                Sector? sector = await _context.Sectors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.Id == sectorId &&
                        s.NetworkId.HasValue &&
                        sectorScope.Contains(s.NetworkId.Value));

                if (sector != null && !string.IsNullOrEmpty(sector.NetworkMask))
                {
                    return Json(new { success = true, networkMask = sector.NetworkMask });
                }
                return Json(new { success = false, message = "القطاع غير موجود أو لا يحتوي على قناع شبكة" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // API: توليد IP التالي للمستقبل بناءً على القطاع
        [HttpGet]
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> GetNextReceiverIP(int sectorId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                IList<string> userRoles = user != null
                    ? await _userManager.GetRolesAsync(user)
                    : Array.Empty<string>();
                bool isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
                IReadOnlyCollection<int> sectorScope = await ResolveReceiverSectorScopeAsync(networkId.Value, isCompanyAdmin);

                // التحقق من أن القطاع ضمن نطاق الشبكة/الشركة
                Sector? sector = await _context.Sectors
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s =>
                        s.Id == sectorId &&
                        s.NetworkId.HasValue &&
                        sectorScope.Contains(s.NetworkId.Value));

                if (sector == null)
                {
                    return Json(new { success = false, message = "القطاع غير موجود في هذه الشبكة" });
                }

                // آخر مستقبل مُدخل لنفس القطاع (بحسب تاريخ الإدخال ثم المعرف)
                Receiver? lastReceiver = await _context.Receivers
                    .AsNoTracking()
                    .Where(r => r.SectorId == sectorId)
                    .OrderByDescending(r => r.CreatedDate)
                    .ThenByDescending(r => r.Id)
                    .FirstOrDefaultAsync();

                string nextIP;

                if (lastReceiver != null)
                {
                    nextIP = GenerateNextReceiverIP(lastReceiver.IPAddress);
                }
                else if (!string.IsNullOrEmpty(sector.IPAddress))
                {
                    string[] sectorIPParts = sector.IPAddress.Split('.');
                    nextIP = sectorIPParts.Length == 4
                        ? $"{sectorIPParts[0]}.{sectorIPParts[1]}.{sectorIPParts[2]}.100"
                        : "192.168.1.100";
                }
                else
                {
                    nextIP = "192.168.1.100";
                }

                return Json(new
                {
                    success = true,
                    nextIP,
                    networkMask = sector.NetworkMask
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // دالة لحساب IP التالي للمستقبل
        private static string GenerateNextReceiverIP(string? currentIP)
        {
            // حماية من القيم الفارغة أو التنسيق غير الصحيح
            if (string.IsNullOrWhiteSpace(currentIP))
            {
                return "192.168.1.100";
            }

            string[] parts = currentIP.Split('.');
            if (parts.Length != 4)
            {
                return "192.168.1.100";
            }

            // محاولة تحويل الأجزاء إلى أرقام مع fallback آمن
            if (!int.TryParse(parts[0], out int part0) ||
                !int.TryParse(parts[1], out int part1) ||
                !int.TryParse(parts[2], out int part2) ||
                !int.TryParse(parts[3], out int part3))
            {
                return "192.168.1.100";
            }

            // زيادة الجزء الأخير
            part3++;

            // إذا تجاوز الجزء الأخير 254، نعيده إلى 100 ونزيد الجزء الثالث
            if (part3 > 254)
            {
                part3 = 100;
                part2++;

                // إذا تجاوز الجزء الثالث 254، نعيده إلى 0 ونزيد الجزء الثاني
                if (part2 > 254)
                {
                    part2 = 0;
                    part1++;

                    // إذا تجاوز الجزء الثاني 254، نعيده إلى 0 ونزيد الجزء الأول
                    if (part1 > 254)
                    {
                        part1 = 0;
                        part0++;

                        // إذا تجاوز الجزء الأول 255، نعيده إلى 192
                        if (part0 > 255)
                        {
                            part0 = 192;
                        }
                    }
                }
            }

            return $"{part0}.{part1}.{part2}.{part3}";
        }

        /// <summary>جلب ارتفاع سطح البحر عند نقطة (لتعبئة نموذج المستقبل تلقائياً من الخريطة).</summary>
        [HttpGet]
        [RequirePermission("Receivers.Create")]
        public async Task<IActionResult> GetElevationAtPoint(double lat, double lon, int? sectorId, CancellationToken ct)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
            }

            double? elevation = await _lineOfSightAnalysisService.LookupElevationAtAsync(lat, lon, ct);
            if (elevation == null)
            {
                if (sectorId.HasValue && sectorId.Value > 0)
                {
                    double? fallbackSectorElevation = await _context.Sectors
                        .AsNoTracking()
                        .Where(s => s.Id == sectorId.Value && s.NetworkId == networkId.Value)
                        .Select(s => s.ElevationMeters)
                        .FirstOrDefaultAsync(ct);

                    if (fallbackSectorElevation.HasValue)
                    {
                        return Json(new
                        {
                            success = true,
                            elevationMeters = Math.Round(fallbackSectorElevation.Value, 1),
                            isFallback = true
                        });
                    }
                }

                return Json(new { success = false, message = "تعذر جلب الارتفاع من الخدمة الخارجية." });
            }

            return Json(new { success = true, elevationMeters = Math.Round(elevation.Value, 1) });
        }

        /// <summary>تحليل تقريبي لخط الرؤية فوق التضاريس مع احتساب المبانٍ من OSM عند توفرها.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Receivers.Create")]
        public async Task<IActionResult> AnalyzeLineOfSight([FromBody] AnalyzeLineOfSightRequest? request, CancellationToken ct)
        {
            if (request == null || request.SectorId <= 0)
            {
                return Json(new { success = false, message = "بيانات الطلب غير صالحة." });
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
            }

            Sector? sector = await _context.Sectors.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SectorId && s.NetworkId == networkId.Value, ct);

            if (sector == null)
            {
                return Json(new { success = false, message = "القطاع غير موجود." });
            }

            LineOfSightAnalysisInput input = new LineOfSightAnalysisInput
            {
                SectorLat = sector.Latitude,
                SectorLon = sector.Longitude,
                SectorTerrainElevationMeters = sector.ElevationMeters,
                SectorAntennaAglMeters = sector.AntennaHeightAglMeters ?? 0,
                ReceiverLat = request.ReceiverLatitude,
                ReceiverLon = request.ReceiverLongitude,
                ReceiverTerrainElevationMeters = request.ReceiverElevationMeters,
                ReceiverAntennaAglMeters = request.ReceiverAntennaHeightAglMeters ?? 0,
                SampleCount = 48
            };

            LineOfSightResult result = await _lineOfSightAnalysisService.AnalyzeAsync(input, ct);

            if (!result.Success)
            {
                return Json(new { success = false, message = result.ErrorMessage ?? "فشل تحليل خط الرؤية." });
            }

            return Json(new { success = true, analysis = result });
        }

        // API: الحصول على معلومات القطاع المحدد (للخريطة)
        [HttpGet]
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> GetSectorInfo(int sectorId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                IList<string> userRoles = user != null
                    ? await _userManager.GetRolesAsync(user)
                    : Array.Empty<string>();
                bool isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
                IReadOnlyCollection<int> sectorScope = await ResolveReceiverSectorScopeAsync(networkId.Value, isCompanyAdmin);

                Sector? sector = await _context.Sectors
                    .Where(s => s.NetworkId.HasValue && sectorScope.Contains(s.NetworkId.Value))
                    .Include(s => s.MikroTikServer)
                    .Include(s => s.Receivers)
                    .FirstOrDefaultAsync(s => s.Id == sectorId);

                if (sector == null)
                {
                    return Json(new { success = false, message = "القطاع غير موجود" });
                }

                List<ReceiverMapPointJson> receivers = sector.Receivers
                    .Select(r => new ReceiverMapPointJson(r.Id, r.Name, r.Latitude, r.Longitude, r.IPAddress))
                    .ToList();

                return Json(new
                {
                    success = true,
                    sector = new
                    {
                        id = sector.Id,
                        name = sector.Name,
                        latitude = sector.Latitude,
                        longitude = sector.Longitude,
                        direction = sector.Direction,
                        coverageAngle = sector.CoverageAngle,
                        coverageRange = sector.CoverageRange,
                        ipAddress = sector.IPAddress,
                        networkMask = sector.NetworkMask,
                        elevationMeters = sector.ElevationMeters,
                        antennaHeightAglMeters = sector.AntennaHeightAglMeters
                    },
                    receivers = receivers
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        private sealed record ReceiverMapPointJson(int id, string? name, double latitude, double longitude, string? ip);

        private sealed record ReceiverMapSectorSummaryJson(
            int id,
            string? name,
            double latitude,
            double longitude,
            double direction,
            double coverageAngle,
            double coverageRange,
            IEnumerable<ReceiverMapPointJson> receivers);

        private sealed record ReceiverCreateMapSectorJson(
            int id,
            string? name,
            int mikrotikServerId,
            double latitude,
            double longitude,
            double direction,
            double coverageAngle,
            double coverageRange,
            string? ipAddress,
            string? networkMask,
            double? elevationMeters,
            double? antennaHeightAglMeters,
            IEnumerable<ReceiverMapPointJson> receivers);

        private bool ReceiverExists(int id)
        {
            return _context.Receivers.Any(e => e.Id == id);
        }

        private async Task<HashSet<int>> GetPendingReceiverIdsAsync(int selectedNetworkId)
        {
            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            List<string> notes = await _context.NetworkServiceRequests
                .AsNoTracking()
                .Where(r =>
                    r.NetworkId == companyNetworkId &&
                    r.Status == NetworkServiceRequestStatus.Pending &&
                    r.FeatureKey == FeatureKeys.Receivers &&
                    r.Notes != null &&
                    r.Notes.StartsWith("EMP_REQ:RECEIVER_"))
                .Select(r => r.Notes!)
                .ToListAsync();

            HashSet<int> ids = new HashSet<int>();
            foreach (string note in notes)
            {
                if (EmployeeApprovalRequestHelper.TryParse(note, out EmployeeApprovalRequestKind kind, out int entityId, out _) &&
                    (kind == EmployeeApprovalRequestKind.ReceiverCreate || kind == EmployeeApprovalRequestKind.ReceiverEdit))
                {
                    ids.Add(entityId);
                }
            }

            return ids;
        }

        private async Task CreateEmployeeApprovalRequestAsync(
            int selectedNetworkId,
            string actorUserId,
            string featureKey,
            string notes,
            decimal expectedChargeAmountSyp = 0m)
        {
            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            NetworkServiceRequest request = new NetworkServiceRequest
            {
                NetworkId = companyNetworkId,
                FeatureKey = featureKey,
                BillingPeriod = PricingBillingPeriod.OneTime,
                AmountSYP = Math.Max(0m, WalletMath.CeilSyp(expectedChargeAmountSyp)),
                AmountUSD = 0m,
                Currency = PricingCurrency.SYP_New,
                Status = NetworkServiceRequestStatus.Pending,
                RequestedByUserId = actorUserId,
                RequestedAt = DateTime.Now,
                Notes = notes
            };
            _context.NetworkServiceRequests.Add(request);
            await _context.SaveChangesAsync();

            await CreateManagerApprovalNotificationsAsync(companyNetworkId, actorUserId, featureKey, request.Id);
        }

        private async Task CreateManagerApprovalNotificationsAsync(
            int companyNetworkId,
            string actorUserId,
            string featureKey,
            int requestId)
        {
            HashSet<string> recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

            string? managerUserId = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId)
                .Select(n => n.ManagerUserId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(managerUserId))
            {
                recipients.Add(managerUserId);
            }

            List<string> roleUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.NetworkId.HasValue && companyScope.Contains(u.NetworkId.Value))
                .Join(_context.UserRoles.AsNoTracking(), u => u.Id, ur => ur.UserId, (u, ur) => new { u.Id, ur.RoleId })
                .Join(_context.Roles.AsNoTracking().Where(r => r.Name == RoleNames.NetworkAdministrator),
                    x => x.RoleId,
                    r => r.Id,
                    (x, _) => x.Id)
                .Distinct()
                .ToListAsync();
            foreach (string uid in roleUserIds)
            {
                recipients.Add(uid);
            }

            if (recipients.Count == 0)
            {
                return;
            }

            string actionLabel = featureKey == FeatureKeys.Receivers ? "المستقبل" : "الخدمة";
            DateTime now = DateTime.Now;
            IEnumerable<UserNotification> rows = recipients.Select(uid => new UserNotification
            {
                Key = $"EmployeeApprovalPending:{featureKey}:{requestId}:{uid}:{Guid.NewGuid():N}",
                UserId = uid,
                NetworkId = companyNetworkId,
                Type = NotificationType.SubscriptionExpiring,
                Title = "طلب موافقة جديد من موظف",
                Message = $"يوجد طلب {actionLabel} من موظف بانتظار اعتمادك.",
                CreatedAt = now,
                IsRead = false
            });

            _context.UserNotifications.AddRange(rows);
            await _context.SaveChangesAsync();
        }
    }
}
