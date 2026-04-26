using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Helpers;
using RadTik.Security;
using RadTik.Services;
using RadTik.ViewModels;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

namespace RadTik.Controllers
{
    // CompanyEmployee هو الدور الجديد للموظف التابع للشركة، و EmployeeLegacy للتوافق.
    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Receivers)]
    public class ReceiverController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ILineOfSightAnalysisService _lineOfSightAnalysisService;

        public ReceiverController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ILineOfSightAnalysisService lineOfSightAnalysisService)
        {
            _context = context;
            _userManager = userManager;
            _usageChargeService = usageChargeService;
            _lineOfSightAnalysisService = lineOfSightAnalysisService;
        }

        // GET: Receiver
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }
            
            // جلب المستقبلات للشبكة الحالية
            var receivers = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .Include(r => r.Clients)
                .OrderByDescending(r => r.Id)
                .ToListAsync();
            ViewBag.PendingReceiverIds = await GetPendingReceiverIdsAsync(networkId.Value);

            // جلب المرسلات (القطاعات) مع المستقبلات الخاصة بها للشبكة الحالية من أجل الخريطة
            var sectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.Receivers)
                .ToListAsync();

            var mapData = sectors.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                latitude = s.Latitude,
                longitude = s.Longitude,
                direction = s.Direction,
                coverageAngle = s.CoverageAngle,
                coverageRange = s.CoverageRange,
                receivers = s.Receivers.Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    latitude = r.Latitude,
                    longitude = r.Longitude,
                    ip = r.IPAddress
                })
            });

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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var receiver = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .Include(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (receiver == null)
            {
                return NotFound();
            }

            var pendingReceiverIds = await GetPendingReceiverIdsAsync(networkId.Value);
            ViewBag.IsPendingReceiverApproval = pendingReceiverIds.Contains(receiver.Id);

            return View(receiver);
        }

        // GET: Receiver/Create
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Receivers.Create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            await PopulateReceiverCreateViewDataAsync(user, networkId.Value, selectedSectorId: null);
            return View();
        }

        // POST: Receiver/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Receivers.Create")]
        public async Task<IActionResult> Create(Receiver receiver)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            var isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
            var companyScope = await ResolveReceiverSectorScopeAsync(networkId.Value, isCompanyAdmin);

            if (ModelState.IsValid)
            {
                // التحقق من أن القطاع يتبع نفس الشبكة
                var sector = await _context.Sectors
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

                // اربط المستقبل بنفس شبكة القطاع المختار لتجنب عدم التطابق ضمن نطاق الشركة.
                receiver.NetworkId = sector.NetworkId;

                var isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy);
                if (isEmployee && user != null)
                {
                    receiver.IsActive = false;
                    _context.Add(receiver);
                    await _context.SaveChangesAsync();

                    var requestNotes = EmployeeApprovalRequestHelper.BuildReceiverCreate(receiver.Id);
                    var pricingNetwork = await _context.Networks
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                    var pricingCompanyNetworkId = pricingNetwork?.ParentNetworkId ?? networkId.Value;
                    var estimate = await _usageChargeService.EstimateImportChargeAsync(
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
                    return RedirectToAction(nameof(Index));
                }

                _context.Add(receiver);
                await _context.SaveChangesAsync();

                var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
                await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, user!.Id, PricingChargeUnit.PerReceiver);

                TempData["Success"] = "تم إضافة المستقبل بنجاح";
                return RedirectToAction(nameof(Index));
            }
            await PopulateReceiverCreateViewDataAsync(user, networkId.Value, receiver.SectorId);
            return View(receiver);
        }

        /// <summary>
        /// بيانات القائمة المنسدلة + JSON الخريطة + IP التالي — مطلوبة في GET وفي POST عند إعادة عرض النموذج.
        /// </summary>
        private async Task PopulateReceiverCreateViewDataAsync(ApplicationUser? user, int networkId, int? selectedSectorId)
        {
            var userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            var isCompanyAdmin = userRoles.Contains(RoleNames.NetworkAdministrator);
            var sectorScope = await ResolveReceiverSectorScopeAsync(networkId, isCompanyAdmin);

            var sectors = await _context.Sectors
                .Where(s => s.NetworkId.HasValue && sectorScope.Contains(s.NetworkId.Value) && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
            ViewData["SectorId"] = new SelectList(sectors, "Id", "Name", selectedSectorId);

            var sectorsForMap = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId.HasValue && sectorScope.Contains(s.NetworkId.Value) && s.IsActive)
                .Include(s => s.Receivers)
                .ToListAsync();

            if (sectors.Count == 0)
            {
                var pendingSectorCount = await _context.NetworkServiceRequests
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

            var mapDataForCreate = sectorsForMap.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                latitude = s.Latitude,
                longitude = s.Longitude,
                direction = s.Direction,
                coverageAngle = s.CoverageAngle,
                coverageRange = s.CoverageRange,
                ipAddress = s.IPAddress,
                networkMask = s.NetworkMask,
                elevationMeters = s.ElevationMeters,
                antennaHeightAglMeters = s.AntennaHeightAglMeters,
                receivers = s.Receivers.Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    latitude = r.Latitude,
                    longitude = r.Longitude,
                    ip = r.IPAddress
                })
            });

            ViewBag.MapDataJson = JsonSerializer.Serialize(mapDataForCreate);

            var lastReceiver = await _context.Receivers
                .Where(r => r.NetworkId == networkId)
                .OrderByDescending(r => r.Id)
                .FirstOrDefaultAsync();

            if (lastReceiver != null)
            {
                ViewBag.NextIP = GenerateNextReceiverIP(lastReceiver.IPAddress);
            }
            else
            {
                ViewBag.NextIP = "192.168.1.100";
            }

            ViewBag.DefaultMask = "255.255.255.0";
            ViewBag.NetworkId = networkId;
            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            await LoadReceiverCreatePricingNoteAsync(networkId);
        }

        private async Task<IReadOnlyCollection<int>> ResolveReceiverSectorScopeAsync(int selectedNetworkId, bool isCompanyAdmin)
        {
            if (!isCompanyAdmin)
            {
                return new[] { selectedNetworkId };
            }

            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;
            return await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);
        }

        private async Task LoadReceiverCreatePricingNoteAsync(int selectedNetworkId)
        {
            try
            {
                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

                var estimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerReceiver,
                    1);

                ViewBag.ReceiverCreateChargeHasPricing = estimate.HasCharge;
                ViewBag.ReceiverCreateChargeAmount = estimate.RequiredAmountSyp;
                ViewBag.ReceiverCreateChargeWalletBalance = estimate.WalletBalance > 0m ? estimate.WalletBalance : 0m;
            }
            catch
            {
                ViewBag.ReceiverCreateChargeHasPricing = false;
                ViewBag.ReceiverCreateChargeAmount = 0m;
                ViewBag.ReceiverCreateChargeWalletBalance = 0m;
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var receiver = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .Include(r => r.Sector)
                    .ThenInclude(s => s.MikroTikServer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (receiver == null)
            {
                return NotFound();
            }
            var sectors = await _context.Sectors
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            // التحقق من أن المستقبل يتبع الشبكة المحددة
            var existingReceiver = await _context.Receivers
                .FirstOrDefaultAsync(r => r.Id == id && r.NetworkId == networkId.Value);
            if (existingReceiver == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var userRoles = user != null
                        ? await _userManager.GetRolesAsync(user)
                        : Array.Empty<string>();
                    var isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy);
                    if (isEmployee && user != null)
                    {
                        var payload = new ReceiverEditApprovalPayload
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
                        var requestNotes = EmployeeApprovalRequestHelper.BuildReceiverEdit(existingReceiver.Id, payload);
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
                    TempData["Success"] = "تم تحديث المستقبل بنجاح";
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
            var sectors = await _context.Sectors
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var receiver = await _context.Receivers
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
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var receiver = await _context.Receivers
                .FirstOrDefaultAsync(r => r.Id == id && r.NetworkId == networkId.Value);
            if (receiver != null)
            {
                _context.Receivers.Remove(receiver);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف المستقبل بنجاح";
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                var sector = await _context.Sectors
                    .FirstOrDefaultAsync(s => s.Id == sectorId && s.NetworkId == networkId.Value);
                
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                // التحقق من أن القطاع يتبع الشبكة المحددة
                var sector = await _context.Sectors
                    .FirstOrDefaultAsync(s => s.Id == sectorId && s.NetworkId == networkId.Value);

                if (sector == null)
                {
                    return Json(new { success = false, message = "القطاع غير موجود في هذه الشبكة" });
                }

                // البحث عن آخر مستقبل لنفس القطاع
                var lastReceiver = await _context.Receivers
                    .Where(r => r.SectorId == sectorId && r.NetworkId == networkId.Value)
                    .OrderByDescending(r => r.Id)
                    .FirstOrDefaultAsync();

                string nextIP;

                if (lastReceiver != null)
                {
                    // إذا وجد مستقبل، توليد IP جديد بناءً على آخر IP
                    nextIP = GenerateNextReceiverIP(lastReceiver.IPAddress);
                }
                else
                {
                    // إذا لم يكن هناك مستقبلات لهذا القطاع، نستخدم IP القطاع
                    if (sector != null && !string.IsNullOrEmpty(sector.IPAddress))
                    {
                        // استخراج الشبكة من IP القطاع
                        var sectorIPParts = sector.IPAddress.Split('.');
                        nextIP = $"{sectorIPParts[0]}.{sectorIPParts[1]}.{sectorIPParts[2]}.100";
                    }
                    else
                    {
                        nextIP = "192.168.1.100";
                    }
                }

                return Json(new { success = true, nextIP = nextIP });
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

            var parts = currentIP.Split('.');
            if (parts.Length != 4)
            {
                return "192.168.1.100";
            }

            // محاولة تحويل الأجزاء إلى أرقام مع fallback آمن
            if (!int.TryParse(parts[0], out var part0) ||
                !int.TryParse(parts[1], out var part1) ||
                !int.TryParse(parts[2], out var part2) ||
                !int.TryParse(parts[3], out var part3))
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
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> GetElevationAtPoint(double lat, double lon, CancellationToken ct)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
            }

            var elevation = await _lineOfSightAnalysisService.LookupElevationAtAsync(lat, lon, ct);
            if (elevation == null)
            {
                return Json(new { success = false, message = "تعذر جلب الارتفاع من الخدمة الخارجية." });
            }

            return Json(new { success = true, elevationMeters = Math.Round(elevation.Value, 1) });
        }

        /// <summary>تحليل تقريبي لخط الرؤية فوق التضاريس مع احتساب المبانٍ من OSM عند توفرها.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Receivers.View")]
        public async Task<IActionResult> AnalyzeLineOfSight([FromBody] AnalyzeLineOfSightRequest? request, CancellationToken ct)
        {
            if (request == null || request.SectorId <= 0)
            {
                return Json(new { success = false, message = "بيانات الطلب غير صالحة." });
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
            }

            var sector = await _context.Sectors.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == request.SectorId && s.NetworkId == networkId.Value, ct);

            if (sector == null)
            {
                return Json(new { success = false, message = "القطاع غير موجود." });
            }

            var input = new LineOfSightAnalysisInput
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

            var result = await _lineOfSightAnalysisService.AnalyzeAsync(input, ct);

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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                var sector = await _context.Sectors
                    .Where(s => s.NetworkId == networkId.Value)
                    .Include(s => s.MikroTikServer)
                    .Include(s => s.Receivers)
                    .FirstOrDefaultAsync(s => s.Id == sectorId);

                if (sector == null)
                {
                    return Json(new { success = false, message = "القطاع غير موجود" });
                }

                var receivers = sector.Receivers.Select(r => new
                {
                    id = r.Id,
                    name = r.Name,
                    latitude = r.Latitude,
                    longitude = r.Longitude,
                    ip = r.IPAddress
                }).ToList();

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

        private bool ReceiverExists(int id)
        {
            return _context.Receivers.Any(e => e.Id == id);
        }

        private async Task<HashSet<int>> GetPendingReceiverIdsAsync(int selectedNetworkId)
        {
            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            var notes = await _context.NetworkServiceRequests
                .AsNoTracking()
                .Where(r =>
                    r.NetworkId == companyNetworkId &&
                    r.Status == NetworkServiceRequestStatus.Pending &&
                    r.FeatureKey == FeatureKeys.Receivers &&
                    r.Notes != null &&
                    r.Notes.StartsWith("EMP_REQ:RECEIVER_"))
                .Select(r => r.Notes!)
                .ToListAsync();

            var ids = new HashSet<int>();
            foreach (var note in notes)
            {
                if (EmployeeApprovalRequestHelper.TryParse(note, out var kind, out var entityId, out _) &&
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
            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            var request = new NetworkServiceRequest
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
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

            var managerUserId = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId)
                .Select(n => n.ManagerUserId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(managerUserId))
            {
                recipients.Add(managerUserId);
            }

            var roleUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.NetworkId.HasValue && companyScope.Contains(u.NetworkId.Value))
                .Join(_context.UserRoles.AsNoTracking(), u => u.Id, ur => ur.UserId, (u, ur) => new { u.Id, ur.RoleId })
                .Join(_context.Roles.AsNoTracking().Where(r => r.Name == RoleNames.NetworkAdministrator),
                    x => x.RoleId,
                    r => r.Id,
                    (x, _) => x.Id)
                .Distinct()
                .ToListAsync();
            foreach (var uid in roleUserIds)
            {
                recipients.Add(uid);
            }

            if (recipients.Count == 0)
            {
                return;
            }

            var actionLabel = featureKey == FeatureKeys.Receivers ? "المستقبل" : "الخدمة";
            var now = DateTime.Now;
            var rows = recipients.Select(uid => new UserNotification
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