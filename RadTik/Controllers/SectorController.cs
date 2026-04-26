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
using RadTik.Services.PricingPolicies;
using RadTik.Services.SectorRadio;
using RadTik.ViewModels.Sector;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RadTik.Controllers
{
    // CompanyEmployee هو الدور الجديد للموظف التابع للشركة، و EmployeeLegacy للتوافق.
    [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Sectors)]
    public class SectorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ISenderPricingOrchestrator _senderPricingOrchestrator;
        private readonly IMikroTikUsersService _mikroTikService;
        private readonly ISectorRadioMetricsQueue _sectorRadioQueue;
        private static readonly HttpClient ElevationHttpClient = new()
        {
            Timeout = TimeSpan.FromSeconds(8)
        };

        public SectorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ISenderPricingOrchestrator senderPricingOrchestrator,
            IMikroTikUsersService mikroTikService,
            ISectorRadioMetricsQueue sectorRadioQueue)
        {
            _context = context;
            _userManager = userManager;
            _usageChargeService = usageChargeService;
            _senderPricingOrchestrator = senderPricingOrchestrator;
            _mikroTikService = mikroTikService;
            _sectorRadioQueue = sectorRadioQueue;
        }

        // GET: Sector
        [RequirePermission("Sectors.View")]
        public virtual async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var sectors = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var receiverCounts = await _context.Receivers
                .AsNoTracking()
                .Where(r => r.Sector!.NetworkId == networkId.Value)
                .GroupBy(r => r.SectorId)
                .Select(g => new { SectorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectorId, x => x.Count);

            var userCountsBySector = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Receiver != null && c.Receiver.Sector.NetworkId == networkId.Value)
                .GroupBy(c => c.Receiver!.SectorId)
                .Select(g => new { SectorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectorId, x => x.Count);

            ViewBag.SectorReceiverCounts = receiverCounts;
            ViewBag.SectorUserCounts = userCountsBySector;
            ViewBag.TotalReceivers = receiverCounts.Values.Sum();
            ViewBag.TotalUsers = userCountsBySector.Values.Sum();

            var importServers = await _context.MikroTikServers
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
            ViewBag.SectorImportServers = importServers;

            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            // معاينة الاستيراد تُحمَّل عبر AJAX بعد عرض الصفحة — تجنباً لانتظار اتصال كل سيرفر MikroTik عند كل فتح للتبويب.
            ViewBag.ImportPreviewByServer = new Dictionary<int, ImportSectorsPreviewResult>();
            ViewBag.ImportChargeByServer = new Dictionary<int, UsageImportChargeEstimate>();

            var baseUnitEstimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSector,
                1);
            ViewBag.SectorImportUnitPrice = baseUnitEstimate.UnitPriceSyp;

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = networkId.Value;
            return View(sectors);
        }

        /// <summary>
        /// معاينة استيراد القطاعات من MikroTik (تُستدعى بعد تحميل الصفحة) لتفادي حظر الاستجابة باتصالات API المتعددة.
        /// </summary>
        [HttpGet]
        [RequirePermission("Sectors.View")]
        public async Task<IActionResult> GetImportPreviewsData(CancellationToken cancellationToken = default)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { ok = false, error = "no_network" });
            }

            var importServers = await _context.MikroTikServers
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);

            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value, cancellationToken);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            var baseUnitEstimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSector,
                1);

            if (importServers.Count == 0)
            {
                return Json(new
                {
                    ok = true,
                    unitPrice = baseUnitEstimate.UnitPriceSyp,
                    servers = Array.Empty<object>()
                });
            }

            // يجب تنفيذ المعاينات بشكل متسلسل: DbContext غير آمن للاستخدام المتوازي، وTask.WhenAll كان يشغّل عدة استعلامات دفعة واحدة.
            var completed = new List<(RadTik.Models.MikroTikServer server, RadTik.Services.ImportSectorsPreviewResult preview, RadTik.Services.UsageImportChargeEstimate estimate)>();
            foreach (var server in importServers)
            {
                var preview = await _mikroTikService.BuildSectorsImportPreviewAsync(server.Id, networkId.Value);
                var estimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSector,
                    preview.ImportableSectorsCount);
                completed.Add((server, preview, estimate));
            }

            var servers = completed.Select(x => new
            {
                serverId = x.server.Id,
                serverName = x.server.Name,
                host = x.server.Host,
                totalInterfaces = x.preview.TotalInterfacesOnServer,
                importable = x.preview.ImportableSectorsCount,
                existing = x.preview.ExistingSectorsCount,
                missingIp = x.preview.MissingIpCount,
                unsupported = x.preview.IsRadioInterfaceCommandUnsupported,
                previewNote = x.preview.PreviewNote,
                unitPrice = x.estimate.UnitPriceSyp,
                totalCharge = x.estimate.RequiredAmountSyp,
                wallet = x.estimate.WalletBalance
            }).ToList();

            return Json(new
            {
                ok = true,
                unitPrice = baseUnitEstimate.UnitPriceSyp,
                servers
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Sectors.Create")]
        public async Task<IActionResult> ImportFromMikroTik(int mikroTikServerId)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
            var preview = await _mikroTikService.BuildSectorsImportPreviewAsync(mikroTikServerId, networkId.Value);
            if (preview.ImportableSectorsCount <= 0)
            {
                TempData["Error"] = "لا توجد قطاعات جديدة قابلة للاستيراد من هذا السيرفر حالياً.";
                return RedirectToAction(nameof(Index));
            }

            var estimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSector,
                preview.ImportableSectorsCount);
            if (estimate.HasCharge && !estimate.HasSufficientBalance)
            {
                TempData["Error"] = $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({estimate.WalletBalance:N2}) أقل من المبلغ المطلوب ({estimate.RequiredAmountSyp:N2}) ل.س.ج.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _mikroTikService.ImportSectorsFromMikroTikAsync(mikroTikServerId, networkId.Value);
            if (result.Success)
            {
                if (result.AddedCount > 0 && user != null)
                {
                    await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, user.Id, PricingChargeUnit.PerSector);
                }
                TempData["Success"] = result.Message;
                if (result.Errors.Any())
                {
                    TempData["ImportSectorWarnings"] = string.Join(" | ", result.Errors.Take(8));
                }
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Sector/Details/5
        [RequirePermission("Sectors.View")]
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

            var sector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .Include(s => s.Receivers)
                .ThenInclude(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sector == null)
            {
                return NotFound();
            }

            return View(sector);
        }

        // GET: Sector/Create
        [RequirePermission("Sectors.Create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            // جلب قائمة خوادم MikroTik للشبكة المحددة
            var servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            ViewBag.MikroTikServers = new SelectList(servers, "Id", "Name");

            // توليد IP تلقائي
            var lastSector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            if (lastSector != null)
            {
                ViewBag.NextIP = GenerateNextIP(lastSector.IPAddress ?? "10.1.1.10");
            }
            else
            {
                ViewBag.NextIP = "10.1.1.10";
            }

            ViewBag.NetworkId = networkId.Value;
            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            return View();
        }

        // POST: Sector/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Sectors.Create")]
        public async Task<IActionResult> Create(Sector sector)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            if (ModelState.IsValid)
            {
                // ربط القطاع بالشبكة
                sector.NetworkId = networkId.Value;

                // التحقق من وجود خادم MikroTik في نفس الشبكة
                var mikrotikServer = await _context.MikroTikServers
                    .FirstOrDefaultAsync(m => m.Id == sector.MikroTikServerId && m.NetworkId == networkId.Value);

                if (mikrotikServer == null)
                {
                    ModelState.AddModelError("MikroTikServerId", "خادم MikroTik غير موجود في هذه الشبكة");
                    var servers = await _context.MikroTikServers
                        .Where(s => s.NetworkId == networkId.Value)
                        .ToListAsync();
                    ViewBag.MikroTikServers = new SelectList(servers, "Id", "Name");
                    ViewBag.NetworkId = networkId.Value;
                    return View(sector);
                }

                var currentArea = Convert.ToString(RouteData.Values["area"]) ?? string.Empty;
                var isEmployeeAreaRequest = string.Equals(currentArea, "CompanyEmployee", StringComparison.OrdinalIgnoreCase);
                var isEmployeePathRequest = (Request.Path.Value ?? string.Empty)
                    .StartsWith("/employee/", StringComparison.OrdinalIgnoreCase);
                var isCompanyEmployee =
                    isEmployeeAreaRequest ||
                    isEmployeePathRequest ||
                    await _userManager.IsInRoleAsync(user!, RoleNames.CompanyEmployee) ||
                    await _userManager.IsInRoleAsync(user!, RoleNames.EmployeeLegacy);

                var outcome = await _senderPricingOrchestrator.HandleSectorCreationAsync(
                    sector,
                    networkId.Value,
                    user!.Id,
                    isCompanyEmployee);

                if (!outcome.Success)
                {
                    TempData["Error"] = outcome.ErrorMessage ?? "تعذر تنفيذ عملية إضافة المرسل حالياً.";
                    return RedirectToAction(nameof(Index));
                }

                TempData["Success"] = outcome.Message;
                return RedirectToAction(nameof(Index));
            }

            var allServers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            ViewBag.MikroTikServers = new SelectList(allServers, "Id", "Name");
            ViewBag.NetworkId = networkId.Value;
            return View(sector);
        }

        // GET: Sector/Edit/5
        [RequirePermission("Sectors.Edit")]
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

            var sector = await _context.Sectors
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            if (sector == null)
            {
                return NotFound();
            }

            var servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            ViewBag.MikroTikServers = new SelectList(servers, "Id", "Name", sector.MikroTikServerId);
            ViewBag.NetworkId = networkId.Value;
            return View(sector);
        }

        // POST: Sector/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Sectors.Edit")]
        public async Task<IActionResult> Edit(int id, Sector sector)
        {
            if (id != sector.Id)
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

            // التحقق من أن القطاع يتبع الشبكة المحددة
            var existingSector = await _context.Sectors
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            if (existingSector == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    sector.NetworkId = networkId.Value; // التأكد من ربطه بالشبكة
                    // لا تستخدم Update(sector): existingSector محمّل ومُتتبّع بالفعل — نسخ القيم إليه فقط
                    _context.Entry(existingSector).CurrentValues.SetValues(sector);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث القطاع بنجاح";
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SectorExists(sector.Id))
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
            var servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            ViewBag.MikroTikServers = new SelectList(servers, "Id", "Name", sector.MikroTikServerId);
            ViewBag.NetworkId = networkId.Value;
            return View(sector);
        }

        // GET: Sector/Delete/5
        [RequirePermission("Sectors.Delete")]
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

            var sector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (sector == null)
            {
                return NotFound();
            }

            return View(sector);
        }

        // POST: Sector/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission("Sectors.Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var sector = await _context.Sectors
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            if (sector != null)
            {
                _context.Sectors.Remove(sector);
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حذف القطاع بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        // GET: Sector/Receivers/5 - عرض المستقبلات التابعة للقطاع
        [RequirePermission("Sectors.View")]
        public async Task<IActionResult> Receivers(int? id)
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

            var sector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.Receivers)
                .ThenInclude(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sector == null)
            {
                return NotFound();
            }

            ViewBag.SectorName = sector.Name;
            return View(sector.Receivers.ToList());
        }

        // GET: Sector/Users/5 - عرض المستخدمين التابعين للقطاع
        [RequirePermission("Sectors.View")]
        public async Task<IActionResult> Users(int? id)
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

            var sector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.Receivers)
                .ThenInclude(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sector == null)
            {
                return NotFound();
            }

            var users = sector.Receivers
                .SelectMany(r => r.Clients)
                .ToList();

            ViewBag.SectorName = sector.Name;
            ViewBag.SectorId = sector.Id;
            return View(users);
        }

        // دالة لحساب IP التالي بناءً على آخر IP في نفس خادم MikroTik
        [HttpGet]
        [RequirePermission("Sectors.View")]
        public async Task<IActionResult> GetNextIP(int mikrotikServerId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == mikrotikServerId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    return Json(new { success = false, message = "الخادم غير موجود في هذه الشبكة" });
                }

                // البحث عن آخر قطاع لنفس خادم MikroTik في نفس الشبكة
                var lastSector = await _context.Sectors
                    .Where(s => s.MikroTikServerId == mikrotikServerId && s.NetworkId == networkId.Value)
                    .OrderByDescending(s => s.Id)
                    .FirstOrDefaultAsync();

                string nextIP;

                if (lastSector != null)
                {
                    // إذا وجد قطاع، توليد IP جديد بناءً على آخر IP
                    nextIP = GenerateNextIP(lastSector.IPAddress ?? "10.1.1.10");
                }
                else
                {
                    // إذا لم يكن هناك قطاعات لهذا الخادم، نبدأ بـ 10.1.1.10
                    nextIP = "10.1.1.10";
                }

                return Json(new { success = true, nextIP = nextIP });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        [RequirePermission("Sectors.Create")]
        public async Task<IActionResult> GetElevation(double lat, double lng)
        {
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
            {
                return Json(new { success = false, message = "إحداثيات الموقع غير صالحة." });
            }

            try
            {
                var requestUrl = $"https://api.open-elevation.com/api/v1/lookup?locations={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                using var response = await ElevationHttpClient.GetAsync(requestUrl);
                if (!response.IsSuccessStatusCode)
                {
                    return Json(new { success = false, message = "تعذر الوصول إلى خدمة الارتفاع حالياً." });
                }

                await using var stream = await response.Content.ReadAsStreamAsync();
                using var document = await JsonDocument.ParseAsync(stream);
                if (!document.RootElement.TryGetProperty("results", out var results) || results.GetArrayLength() == 0)
                {
                    return Json(new { success = false, message = "لم يتم العثور على بيانات ارتفاع لهذا الموقع." });
                }

                var elevation = results[0].GetProperty("elevation").GetDouble();
                return Json(new { success = true, elevation = Math.Round(elevation, 2) });
            }
            catch
            {
                return Json(new { success = false, message = "حدث خطأ أثناء جلب الارتفاع من الخدمة الخارجية." });
            }
        }

        // GET: Sector/RadioEngineering
        [RequirePermission("Sectors.View")]
        public async Task<IActionResult> RadioEngineering()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var sectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .AsNoTracking()
                .ToListAsync();

            var viewModel = new RadioEngineeringStudyViewModel
            {
                TotalSectors = sectors.Count,
                ActiveSectors = sectors.Count(s => s.IsActive),
                ReadySectors = sectors.Count(s =>
                    s.IsActive &&
                    !string.IsNullOrWhiteSpace(s.IPAddress) &&
                    s.MikroTikServerId > 0 &&
                    s.MikroTikServer != null &&
                    s.MikroTikServer.IsActive),
                MissingIpSectors = sectors.Count(s => string.IsNullOrWhiteSpace(s.IPAddress)),
                MissingServerSectors = sectors.Count(s => s.MikroTikServer == null),
                InactiveServersLinkedSectors = sectors.Count(s => s.MikroTikServer != null && !s.MikroTikServer.IsActive)
            };

            viewModel.ServerProfiles = sectors
                .Where(s => s.MikroTikServer != null)
                .GroupBy(s => DetectServerProfile(s.MikroTikServer!.Name))
                .OrderByDescending(g => g.Count())
                .Select(g => new StudyStatItem
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Note = "توزيع القطاعات حسب عائلة سيرفر MikroTik"
                })
                .ToList();

            viewModel.SectorFamilies = sectors
                .GroupBy(s => DetectSectorFamily(s.Name))
                .OrderByDescending(g => g.Count())
                .Select(g => new StudyStatItem
                {
                    Name = g.Key,
                    Count = g.Count(),
                    Note = "تصنيف تقريبي بالاعتماد على اسم القطاع"
                })
                .ToList();

            viewModel.Scenarios =
            [
                new StudyScenario
                {
                    Name = "سيناريو المراقبة اللحظية",
                    Goal = "عرض تردد/تشويش القطاع كل 1-5 دقائق دون تعديل الإعدادات.",
                    Preconditions = "IP إدارة متاح، ربط القطاع مع سيرفر MikroTik، صلاحية قراءة.",
                    ExecutionFlow = "Job قراءة → Adapter جهازي → حفظ Metrics → تحديث Dashboard + تنبيه.",
                    SuccessKpi = "تغطية قياسات حديثة >= 95% من القطاعات النشطة.",
                    RiskLevel = "منخفض"
                },
                new StudyScenario
                {
                    Name = "سيناريو تغيير التردد المراقَب",
                    Goal = "تنفيذ تغيير التردد بشكل آمن عبر طلب/موافقة/تحقق/Rollback.",
                    Preconditions = "صلاحية RadioControl، تفعيل سجل تدقيق، جاهزية rollback.",
                    ExecutionFlow = "Create Request → Approve → Execute Worker → Verify 60 ثانية → Success أو Rollback.",
                    SuccessKpi = "نسبة نجاح تغييرات مع تحقق نهائي >= 98%.",
                    RiskLevel = "مرتفع"
                },
                new StudyScenario
                {
                    Name = "سيناريو الانقطاع أو ضعف الوصول",
                    Goal = "منع التأثير التشغيلي عند فقدان الوصول لقطاع أو فشل أمر.",
                    Preconditions = "Timeout + Retry + Circuit breaker + Alerting.",
                    ExecutionFlow = "فشل تنفيذ → تسجيل OperationLog → إعادة محاولة محدودة → تنبيه مدير النظام.",
                    SuccessKpi = "اكتشاف الفشل وإخطار الفريق خلال أقل من 2 دقيقة.",
                    RiskLevel = "متوسط"
                }
            ];

            viewModel.Phases =
            [
                new StudyPhase
                {
                    Name = "Phase 0 - Discovery / PoC",
                    TimeEstimate = "5-7 أيام",
                    Tasks =
                    [
                        "تحديد أنواع القطاعات الفعلية وربطها بـ Adapter مناسب.",
                        "تجربة قراءة metrics على عينات 3 أنواع مختلفة.",
                        "توثيق فروقات الأوامر بين الأجهزة/الإصدارات."
                    ],
                    Output = "تقرير توافق + قائمة قدرات مدعومة لكل نوع قطاع."
                },
                new StudyPhase
                {
                    Name = "Phase 1 - Monitoring Only",
                    TimeEstimate = "10-14 يوم",
                    Tasks =
                    [
                        "إنشاء نماذج بيانات SectorRadioMetricSample وواجهات Dashboard.",
                        "تفعيل Polling Jobs دورية مع تنبيهات العتبات.",
                        "اعتماد مؤشرات أداء وتجربة مستخدم متسقة مع هوية النظام."
                    ],
                    Output = "مراقبة دقيقة قابلة للتشغيل دون أي تغيير إعدادات."
                },
                new StudyPhase
                {
                    Name = "Phase 2 - Controlled Radio Control",
                    TimeEstimate = "12-16 يوم",
                    Tasks =
                    [
                        "إنشاء Workflow طلب تغيير التردد + موافقات.",
                        "تنفيذ Worker آمن مع Verify وRollback تلقائي.",
                        "إطلاق صفحة عمليات راديوية وسجل تدقيق مفصل."
                    ],
                    Output = "تحكم مباشر وآمن بإعدادات الراديو مع استدامة تشغيلية."
                }
            ];

            viewModel.RiskControls =
            [
                "منع أي عملية write مباشرة من الواجهة؛ التنفيذ فقط عبر Queue/Worker.",
                "قفل قطاع واحد أثناء التنفيذ (Single sector lock).",
                "Snapshot قبل التعديل وإرجاع تلقائي عند فشل التحقق.",
                "توثيق Audit كامل (المستخدم، الوقت، الإجراء، النتيجة)."
            ];

            viewModel.PerformanceGuidelines =
            [
                "جدولة Polling متدرجة لتقليل الحمل على السيرفرات.",
                "إيقاف Auto-refresh عالي التردد في الصفحات الكبيرة إلا عند الحاجة.",
                "استخدام AsNoTracking في قراءات المراقبة والتحليلات.",
                "تخزين مختصر للـ Raw payload وتطبيق سياسة Retention."
            ];

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = networkId.Value;
            return View(viewModel);
        }

        // GET: Sector/RadioMonitoring
        [RequirePermission("Sectors.View")]
        public async Task<IActionResult> RadioMonitoring()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var sectors = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .ToListAsync();

            // Allow manual refresh trigger (PoC).
            if (Request.Query.ContainsKey("refresh"))
            {
                foreach (var s in sectors.Where(x => x.MikroTikServerId > 0))
                {
                    await _sectorRadioQueue.EnqueueAsync(new SectorRadioMetricsJob
                    {
                        SectorId = s.Id,
                        MikroTikServerId = s.MikroTikServerId
                    });
                }
                TempData["Success"] = "تمت جدولة تحديث القياسات للقطاعات النشطة.";
                return RedirectToAction(nameof(RadioMonitoring));
            }

            var sectorIds = sectors.Select(s => s.Id).ToList();
            var latestBySector = await _context.SectorRadioMetricSamples
                .AsNoTracking()
                .Where(x => sectorIds.Contains(x.SectorId))
                .GroupBy(x => x.SectorId)
                .Select(g => g.OrderByDescending(x => x.CapturedAt).FirstOrDefault())
                .ToListAsync();

            var latestMap = latestBySector
                .Where(x => x != null)
                .ToDictionary(x => x!.SectorId, x => x!);

            var latestAlertsBySector = await _context.SectorRadioEvents
                .AsNoTracking()
                .Where(e => sectorIds.Contains(e.SectorId))
                .GroupBy(e => e.SectorId)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).FirstOrDefault())
                .ToListAsync();

            var latestAlertMap = latestAlertsBySector
                .Where(x => x != null)
                .ToDictionary(x => x!.SectorId, x => x!);

            var rows = sectors
                .OrderBy(s => s.Name)
                .Select(s =>
                {
                    latestMap.TryGetValue(s.Id, out var sample);
                    return new SectorRadioMonitoringRow
                    {
                        SectorId = s.Id,
                        SectorName = s.Name ?? $"Sector #{s.Id}",
                        SectorIp = s.IPAddress,
                        ServerName = s.MikroTikServer?.Name ?? "—",
                        CapturedAt = sample?.CapturedAt,
                        FrequencyMhz = sample?.FrequencyMhz,
                        NoiseFloorDbm = sample?.NoiseFloorDbm,
                        SignalDbm = sample?.SignalDbm,
                        SnrDb = sample?.SnrDb,
                        CcqPercent = sample?.CcqPercent,
                        LastSeverity = latestAlertMap.TryGetValue(s.Id, out var alert) ? alert.Severity : null,
                        StatusMessage = sample?.StatusMessage ?? "لا توجد قياسات بعد"
                    };
                })
                .ToList();

            var sixHoursAgo = DateTime.Now.AddHours(-6);
            var trendSamples = await _context.SectorRadioMetricSamples
                .AsNoTracking()
                .Where(x => sectorIds.Contains(x.SectorId) && x.CapturedAt >= sixHoursAgo)
                .OrderBy(x => x.CapturedAt)
                .ToListAsync();

            var trendPoints = trendSamples
                .GroupBy(x => new DateTime(
                    x.CapturedAt.Year,
                    x.CapturedAt.Month,
                    x.CapturedAt.Day,
                    x.CapturedAt.Hour,
                    x.CapturedAt.Minute / 20 * 20,
                    0))
                .OrderBy(g => g.Key)
                .Select(g => new SectorRadioTrendPoint
                {
                    BucketAt = g.Key,
                    AvgSnrDb = g.Where(x => x.SnrDb.HasValue).Select(x => (decimal)x.SnrDb!.Value).DefaultIfEmpty().Average(),
                    AvgNoiseDbm = g.Where(x => x.NoiseFloorDbm.HasValue).Select(x => (decimal)x.NoiseFloorDbm!.Value).DefaultIfEmpty().Average(),
                    AvgCcqPercent = g.Where(x => x.CcqPercent.HasValue).Select(x => (decimal)x.CcqPercent!.Value).DefaultIfEmpty().Average()
                })
                .TakeLast(18)
                .ToList();

            var recentEvents = await _context.SectorRadioEvents
                .AsNoTracking()
                .Where(e => sectorIds.Contains(e.SectorId))
                .OrderByDescending(e => e.CreatedAt)
                .Take(20)
                .Join(
                    _context.Sectors.AsNoTracking(),
                    e => e.SectorId,
                    s => s.Id,
                    (e, s) => new SectorRadioEventRow
                    {
                        Id = e.Id,
                        SectorId = e.SectorId,
                        SectorName = s.Name ?? $"Sector #{s.Id}",
                        Severity = e.Severity,
                        MetricName = e.MetricName,
                        MetricValue = e.MetricValue,
                        ThresholdValue = e.ThresholdValue,
                        Message = e.Message,
                        CreatedAt = e.CreatedAt
                    })
                .ToListAsync();

            var freshThreshold = DateTime.Now.AddMinutes(-10);
            var viewModel = new SectorRadioMonitoringViewModel
            {
                TotalSectors = sectors.Count,
                Rows = rows,
                MetricsFreshCount = rows.Count(r => r.CapturedAt.HasValue && r.CapturedAt.Value >= freshThreshold),
                StaleCount = rows.Count(r => !r.CapturedAt.HasValue || r.CapturedAt.Value < freshThreshold),
                ActiveAlertsCount = recentEvents.Count(e => e.CreatedAt >= DateTime.Now.AddHours(-24)),
                TrendPoints = trendPoints,
                RecentEvents = recentEvents,
                GeneratedAt = DateTime.Now
            };

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = networkId.Value;
            return View(viewModel);
        }

        // دالة لحساب IP التالي
        private string GenerateNextIP(string currentIP)
        {
            var parts = currentIP.Split('.');

            // تحويل الأجزاء إلى أرقام
            int part0 = int.Parse(parts[0]);
            int part1 = int.Parse(parts[1]);
            int part2 = int.Parse(parts[2]);
            int part3 = int.Parse(parts[3]);

            // زيادة الجزء الأخير
            part3++;

            // إذا تجاوز الجزء الأخير 254، نعيده إلى 10 ونزيد الجزء الثالث
            if (part3 > 254)
            {
                part3 = 10;
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

                        // إذا تجاوز الجزء الأول 255، نعيده إلى 10
                        if (part0 > 255)
                        {
                            part0 = 10;
                        }
                    }
                }
            }

            return $"{part0}.{part1}.{part2}.{part3}";
        }

        private bool SectorExists(int id)
        {
            return _context.Sectors.Any(e => e.Id == id);
        }

        private static string DetectServerProfile(string? serverName)
        {
            var value = (serverName ?? string.Empty).ToLowerInvariant();
            if (value.Contains("5009")) return "MikroTik RB5009";
            if (value.Contains("4011")) return "MikroTik RB4011";
            return "MikroTik أخرى";
        }

        private static string DetectSectorFamily(string? sectorName)
        {
            var value = (sectorName ?? string.Empty).ToLowerInvariant();
            if (value.Contains("90")) return "Sector 90";
            if (value.Contains("120")) return "Sector 120";
            if (value.Contains("litebeam")) return "LiteBeam 5AC Gen2";
            return "غير مصنف";
        }
    }
}
