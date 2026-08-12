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
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.SectorRadio;
using RadaTik.ViewModels.Sector;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RadaTik.Controllers
{
    // CompanyEmployee هو الدور الجديد للموظف التابع للشركة، و EmployeeLegacy للتوافق.
    [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Sectors)]
    public class SectorController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ICreatePricingPreviewService _pricingPreviewService;
        private readonly ISenderPricingOrchestrator _senderPricingOrchestrator;
        private readonly IMikroTikSectorService _mikroTikSectorService;
        private readonly ISectorRadioMetricsQueue _sectorRadioQueue;
        private readonly ILineOfSightAnalysisService _lineOfSight;

        public SectorController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ICreatePricingPreviewService pricingPreviewService,
            ISenderPricingOrchestrator senderPricingOrchestrator,
            IMikroTikSectorService mikroTikSectorService,
            ISectorRadioMetricsQueue sectorRadioQueue,
            ILineOfSightAnalysisService lineOfSight)
        {
            _context = context;
            _userManager = userManager;
            _usageChargeService = usageChargeService;
            _pricingPreviewService = pricingPreviewService;
            _senderPricingOrchestrator = senderPricingOrchestrator;
            _mikroTikSectorService = mikroTikSectorService;
            _sectorRadioQueue = sectorRadioQueue;
            _lineOfSight = lineOfSight;
        }

        // GET: Sector
        [RequirePermission("Sectors.View")]
        public virtual async Task<IActionResult> Index()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            List<Sector> sectors = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .OrderBy(s => s.Name)
                .ToListAsync();

            Dictionary<int, int> receiverCounts = await _context.Receivers
                .AsNoTracking()
                .Where(r => r.Sector!.NetworkId == networkId.Value)
                .GroupBy(r => r.SectorId)
                .Select(g => new { SectorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectorId, x => x.Count);

            Dictionary<int, int> userCountsBySector = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Receiver != null && c.Receiver.Sector.NetworkId == networkId.Value)
                .GroupBy(c => c.Receiver!.SectorId)
                .Select(g => new { SectorId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SectorId, x => x.Count);

            ViewBag.SectorReceiverCounts = receiverCounts;
            ViewBag.SectorUserCounts = userCountsBySector;
            ViewBag.TotalReceivers = receiverCounts.Values.Sum();
            ViewBag.TotalUsers = userCountsBySector.Values.Sum();

            List<MikroTikServer> importServers = await _context.MikroTikServers
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync();
            ViewBag.SectorImportServers = importServers;

            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            // معاينة الاستيراد تُحمَّل عبر AJAX بعد عرض الصفحة — تجنباً لانتظار اتصال كل سيرفر MikroTik عند كل فتح للتبويب.
            ViewBag.ImportPreviewByServer = new Dictionary<int, ImportSectorsPreviewResult>();
            ViewBag.ImportChargeByServer = new Dictionary<int, UsageImportChargeEstimate>();

            UsageImportChargeEstimate baseUnitEstimate = await _usageChargeService.EstimateImportChargeAsync(
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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { ok = false, error = "no_network" });
            }

            List<MikroTikServer> importServers = await _context.MikroTikServers
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value && s.IsActive)
                .OrderBy(s => s.Name)
                .ToListAsync(cancellationToken);

            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value, cancellationToken);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            UsageImportChargeEstimate baseUnitEstimate = await _usageChargeService.EstimateImportChargeAsync(
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
            List<(MikroTikServer server, ImportSectorsPreviewResult preview, UsageImportChargeEstimate estimate)> completed = new List<(RadaTik.Models.MikroTikServer server, RadaTik.Services.ImportSectorsPreviewResult preview, RadaTik.Services.UsageImportChargeEstimate estimate)>();
            foreach (MikroTikServer? server in importServers)
            {
                ImportSectorsPreviewResult preview = await _mikroTikSectorService.BuildSectorsImportPreviewAsync(server.Id, networkId.Value);
                UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSector,
                    preview.ImportableSectorsCount);
                completed.Add((server, preview, estimate));
            }

            List<SectorImportPreviewServerJson> servers = completed.Select(x => new SectorImportPreviewServerJson(
                x.server.Id,
                x.server.Name,
                x.server.Host,
                x.preview.TotalInterfacesOnServer,
                x.preview.ImportableSectorsCount,
                x.preview.ExistingSectorsCount,
                x.preview.MissingIpCount,
                x.preview.IsRadioInterfaceCommandUnsupported,
                x.preview.PreviewNote,
                x.estimate.UnitPriceSyp,
                x.estimate.RequiredAmountSyp,
                x.estimate.WalletBalance)).ToList();

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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Index));
            }

            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
            ImportSectorsPreviewResult preview = await _mikroTikSectorService.BuildSectorsImportPreviewAsync(mikroTikServerId, networkId.Value);
            if (preview.ImportableSectorsCount <= 0)
            {
                TempData["Error"] = "لا توجد قطاعات جديدة قابلة للاستيراد من هذا السيرفر حالياً.";
                return RedirectToAction(nameof(Index));
            }

            UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSector,
                preview.ImportableSectorsCount);
            if (estimate.HasCharge && !estimate.HasSufficientBalance)
            {
                TempData["Error"] = $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({estimate.WalletBalance:N2}) أقل من المبلغ المطلوب ({estimate.RequiredAmountSyp:N2}) ل.س.ج.";
                return RedirectToAction(nameof(Index));
            }

            ImportSectorsResult result = await _mikroTikSectorService.ImportSectorsFromMikroTikAsync(mikroTikServerId, networkId.Value);
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

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Sector? sector = await _context.Sectors
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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // جلب قائمة خوادم MikroTik للشبكة المحددة
            List<MikroTikServer> servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            ViewBag.MikroTikServers = new SelectList(servers, "Id", "Name");

            // توليد IP تلقائي
            Sector? lastSector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            string nextIp;
            if (lastSector != null)
            {
                nextIp = GenerateNextIP(lastSector.IPAddress ?? "10.1.1.10");
            }
            else
            {
                nextIp = "10.1.1.10";
            }

            ViewBag.NextIP = nextIp;
            ViewBag.NetworkId = networkId.Value;
            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            await LoadSectorCreatePricingNoteAsync(networkId.Value);
            return View(new Sector
            {
                IPAddress = nextIp,
                NetworkMask = "255.255.255.0"
            });
        }

        // POST: Sector/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Sectors.Create")]
        public async Task<IActionResult> Create(Sector sector)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            if (ModelState.IsValid)
            {
                // ربط القطاع بالشبكة
                sector.NetworkId = networkId.Value;

                // التحقق من وجود خادم MikroTik في نفس الشبكة
                MikroTikServer? mikrotikServer = await _context.MikroTikServers
                    .FirstOrDefaultAsync(m => m.Id == sector.MikroTikServerId && m.NetworkId == networkId.Value);

                if (mikrotikServer == null)
                {
                    ModelState.AddModelError("MikroTikServerId", "خادم MikroTik غير موجود في هذه الشبكة");
                    List<MikroTikServer> servers = await _context.MikroTikServers
                        .Where(s => s.NetworkId == networkId.Value)
                        .ToListAsync();
                    ViewBag.MikroTikServers = new SelectList(servers, "Id", "Name");
                    ViewBag.NetworkId = networkId.Value;
                    await LoadSectorCreatePricingNoteAsync(networkId.Value);
                    return View(sector);
                }

                string currentArea = Convert.ToString(RouteData.Values["area"]) ?? string.Empty;
                bool isEmployeeAreaRequest = string.Equals(currentArea, "CompanyEmployee", StringComparison.OrdinalIgnoreCase);
                bool isEmployeePathRequest = (Request.Path.Value ?? string.Empty)
                    .StartsWith("/employee/", StringComparison.OrdinalIgnoreCase);
                bool isCompanyEmployee =
                    isEmployeeAreaRequest ||
                    isEmployeePathRequest ||
                    await _userManager.IsInRoleAsync(user!, RoleNames.CompanyEmployee) ||
                    await _userManager.IsInRoleAsync(user!, RoleNames.EmployeeLegacy);

                SenderCreateOutcome outcome = await _senderPricingOrchestrator.HandleSectorCreationAsync(
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

            List<MikroTikServer> allServers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            ViewBag.MikroTikServers = new SelectList(allServers, "Id", "Name");
            ViewBag.NetworkId = networkId.Value;
            await LoadSectorCreatePricingNoteAsync(networkId.Value);
            return View(sector);
        }

        private async Task LoadSectorCreatePricingNoteAsync(int selectedNetworkId)
        {
            try
            {
                Network? selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;
                UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSector,
                    1);
                CreatePricingPreviewResult preview = await _pricingPreviewService.BuildAsync(
                    selectedNetworkId,
                    FeatureKeys.Sectors,
                    PricingChargeUnit.PerSector,
                    PricingPreviewCounterKeys.Sectors);
                PricingPreviewViewBagMapper.Apply(ViewData, "SectorCreate", preview);

                ViewBag.SectorCreateChargeHasPricing = estimate.HasCharge;
                ViewBag.SectorCreateChargeAmount = estimate.RequiredAmountSyp;
                ViewBag.SectorCreateChargeWalletBalance = estimate.WalletBalance > 0m ? estimate.WalletBalance : 0m;
            }
            catch
            {
                ViewBag.SectorCreateChargeHasPricing = false;
                ViewBag.SectorCreateChargeAmount = 0m;
                ViewBag.SectorCreateChargeWalletBalance = 0m;
                PricingPreviewViewBagMapper.Apply(ViewData, "SectorCreate", PricingPreviewViewBagMapper.Empty());
            }
        }

        // GET: Sector/Edit/5
        [RequirePermission("Sectors.Edit")]
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

            Sector? sector = await _context.Sectors
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            if (sector == null)
            {
                return NotFound();
            }

            List<MikroTikServer> servers = await _context.MikroTikServers
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

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // التحقق من أن القطاع يتبع الشبكة المحددة
            Sector? existingSector = await _context.Sectors
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
                    TempData["Success"] = AppMessages.OperationSuccess;
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
            List<MikroTikServer> servers = await _context.MikroTikServers
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

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Sector? sector = await _context.Sectors
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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Sector? sector = await _context.Sectors
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            if (sector != null)
            {
                // SQL Server rejects CASCADE here (Sector -> Events + Samples both touch Events via SetNull).
                // Remove radio telemetry explicitly before deleting the sector row.
                await _context.SectorRadioEvents
                    .Where(e => e.SectorId == id)
                    .ExecuteDeleteAsync();
                await _context.SectorRadioMetricSamples
                    .Where(s => s.SectorId == id)
                    .ExecuteDeleteAsync();

                _context.Sectors.Remove(sector);
                await _context.SaveChangesAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
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

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Sector? sector = await _context.Sectors
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

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            Sector? sector = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.Receivers)
                .ThenInclude(r => r.Clients)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (sector == null)
            {
                return NotFound();
            }

            List<Client> users = sector.Receivers
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
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == mikrotikServerId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    return Json(new { success = false, message = "الخادم غير موجود في هذه الشبكة" });
                }

                // البحث عن آخر قطاع لنفس خادم MikroTik في نفس الشبكة
                Sector? lastSector = await _context.Sectors
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
        public async Task<IActionResult> GetElevation(double lat, double lng, CancellationToken ct = default)
        {
            if (lat < -90 || lat > 90 || lng < -180 || lng > 180)
            {
                return Json(new { success = false, message = "إحداثيات الموقع غير صالحة." });
            }

            try
            {
                double? elevation = await _lineOfSight.LookupElevationAtAsync(lat, lng, ct);
                if (!elevation.HasValue)
                {
                    return Json(new
                    {
                        success = false,
                        message = "تعذر الوصول إلى خدمة الارتفاع حالياً. يمكنك إدخال الارتفاع يدوياً."
                    });
                }

                return Json(new { success = true, elevation = Math.Round(elevation.Value, 2) });
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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            List<Sector> sectors = await _context.Sectors
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .AsNoTracking()
                .ToListAsync();

            RadioEngineeringStudyViewModel viewModel = new RadioEngineeringStudyViewModel
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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            List<Sector> sectors = await _context.Sectors
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value)
                .Include(s => s.MikroTikServer)
                .ToListAsync();

            // Allow manual refresh trigger (PoC).
            if (Request.Query.ContainsKey("refresh"))
            {
                foreach (Sector? s in sectors.Where(x => x.MikroTikServerId > 0))
                {
                    await _sectorRadioQueue.EnqueueAsync(new SectorRadioMetricsJob
                    {
                        SectorId = s.Id,
                        MikroTikServerId = s.MikroTikServerId
                    });
                }
                TempData["Success"] = AppMessages.OperationSuccess;
                return RedirectToAction(nameof(RadioMonitoring));
            }

            List<int> sectorIds = sectors.Select(s => s.Id).ToList();
            List<SectorRadioMetricSample?> latestBySector = await _context.SectorRadioMetricSamples
                .AsNoTracking()
                .Where(x => sectorIds.Contains(x.SectorId))
                .GroupBy(x => x.SectorId)
                .Select(g => g.OrderByDescending(x => x.CapturedAt).FirstOrDefault())
                .ToListAsync();

            Dictionary<int, SectorRadioMetricSample> latestMap = latestBySector
                .Where(x => x != null)
                .ToDictionary(x => x!.SectorId, x => x!);

            List<SectorRadioEvent?> latestAlertsBySector = await _context.SectorRadioEvents
                .AsNoTracking()
                .Where(e => sectorIds.Contains(e.SectorId))
                .GroupBy(e => e.SectorId)
                .Select(g => g.OrderByDescending(x => x.CreatedAt).FirstOrDefault())
                .ToListAsync();

            Dictionary<int, SectorRadioEvent> latestAlertMap = latestAlertsBySector
                .Where(x => x != null)
                .ToDictionary(x => x!.SectorId, x => x!);

            List<SectorRadioMonitoringRow> rows = sectors
                .OrderBy(s => s.Name)
                .Select(s =>
                {
                    latestMap.TryGetValue(s.Id, out SectorRadioMetricSample? sample);
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
                        LastSeverity = latestAlertMap.TryGetValue(s.Id, out SectorRadioEvent? alert) ? alert.Severity : null,
                        StatusMessage = sample?.StatusMessage ?? "لا توجد قياسات بعد"
                    };
                })
                .ToList();

            DateTime sixHoursAgo = DateTime.Now.AddHours(-6);
            List<SectorRadioMetricSample> trendSamples = await _context.SectorRadioMetricSamples
                .AsNoTracking()
                .Where(x => sectorIds.Contains(x.SectorId) && x.CapturedAt >= sixHoursAgo)
                .OrderBy(x => x.CapturedAt)
                .ToListAsync();

            List<SectorRadioTrendPoint> trendPoints = trendSamples
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

            List<SectorRadioEventRow> recentEvents = await _context.SectorRadioEvents
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

            DateTime freshThreshold = DateTime.Now.AddMinutes(-10);
            SectorRadioMonitoringViewModel viewModel = new SectorRadioMonitoringViewModel
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
            string[] parts = currentIP.Split('.');

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

        private sealed record SectorImportPreviewServerJson(
            int serverId,
            string? serverName,
            string? host,
            int totalInterfaces,
            int importable,
            int existing,
            int missingIp,
            bool unsupported,
            string? previewNote,
            decimal unitPrice,
            decimal totalCharge,
            decimal wallet);

        private bool SectorExists(int id)
        {
            return _context.Sectors.Any(e => e.Id == id);
        }

        private static string DetectServerProfile(string? serverName)
        {
            string value = (serverName ?? string.Empty).ToLowerInvariant();
            if (value.Contains("5009"))
            {
                return "MikroTik RB5009";
            }

            if (value.Contains("4011"))
            {
                return "MikroTik RB4011";
            }

            return "MikroTik أخرى";
        }

        private static string DetectSectorFamily(string? sectorName)
        {
            string value = (sectorName ?? string.Empty).ToLowerInvariant();
            if (value.Contains("90"))
            {
                return "Sector 90";
            }

            if (value.Contains("120"))
            {
                return "Sector 120";
            }

            if (value.Contains("litebeam"))
            {
                return "LiteBeam 5AC Gen2";
            }

            return "غير مصنف";
        }
    }
}
