using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;

namespace RadaTik.Controllers
{
    /// <summary>
    /// لوحة تحكم مدير النظام - إحصائيات، طلبات الصيانة، نقاط التحصيل
    /// </summary>
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class SystemAdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<SystemAdminController> logger,
        IConfiguration configuration,
        IOnboardingChecklistService onboardingChecklistService) : Controller
    {
        private sealed record NetworkIdCountRow(int NetworkId, int Count);
        private sealed record ServerIdCountRow(int ServerId, int Count);
        private sealed record ReceiverIdCountRow(int ReceiverId, int Count);
        private sealed record SectorIdCountRow(int SectorId, int Count);
        private sealed record MaintenanceCompletedTimingRow(DateTime RequestDate, DateTime? CompletedDate);

        private readonly ApplicationDbContext _context = context;
        private readonly UserManager<ApplicationUser> _userManager = userManager;
        private readonly ILogger<SystemAdminController> _logger = logger;
        private readonly IConfiguration _configuration = configuration;
        private readonly IOnboardingChecklistService _onboardingChecklistService = onboardingChecklistService;

        /// <summary>
        /// الصفحة الرئيسية - تبويبات: لوحة التحكم، طلبات الصيانة، نقاط التحصيل
        /// </summary>
        public async Task<IActionResult> Index(string tab = "dashboard", double? slaHours = null)
        {
            string activeTab = string.IsNullOrEmpty(tab) ? "dashboard" : tab.ToLowerInvariant();
            ViewData["ActiveTab"] = activeTab;

            if (activeTab == "dashboard")
            {
                await LoadDashboardStats();
                await LoadOnboardingChecklistAsync();
                return View("Index");
            }
            if (activeTab == "maintenance")
            {
                double effectiveSlaHours = NormalizeSlaHours(
                    slaHours,
                    ReadMaintenanceSlaHoursFromConfig(defaultValue: 24));

                await LoadMaintenanceStats(effectiveSlaHours);
                return View("Index");
            }
            if (activeTab == "collectionpoints")
            {
                await LoadCollectionPointsData();
                return View("Index");
            }
            if (activeTab == "pricing")
            {
                return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
            }

            await LoadDashboardStats();
            await LoadOnboardingChecklistAsync();
            return View("Index");
        }

        private async Task LoadOnboardingChecklistAsync()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return;
            }

            ViewBag.OnboardingChecklist = await _onboardingChecklistService.GetSystemChecklistAsync(user.Id);
        }

        private double ReadMaintenanceSlaHoursFromConfig(double defaultValue)
        {
            double? v = _configuration.GetValue<double?>("Maintenance:SlaHours");
            return NormalizeSlaHours(v, defaultValue);
        }

        private static double NormalizeSlaHours(double? value, double defaultValue)
        {
            if (!value.HasValue || double.IsNaN(value.Value) || double.IsInfinity(value.Value) || value.Value <= 0)
            {
                return defaultValue;
            }

            // Guard rails: 0.25h (15m) to 720h (30d)
            return Math.Clamp(value.Value, 0.25, 720);
        }

        /// <summary>
        /// إضافة سعر خدمة جديدة (GET)
        /// </summary>
        [HttpGet]
        public IActionResult CreatePricing() =>
            RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });

        /// <summary>
        /// إضافة سعر خدمة (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePricing(PricingItemType itemType, decimal amountSYP, decimal amountUSD, PricingBillingPeriod billingPeriod)
        {
            _logger.LogInformation("Legacy pricing endpoint CreatePricing is deprecated. Redirecting to ServiceCatalog.");
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// تحديث سعر خدمة (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdatePricing(int id, PricingItemType itemType, decimal amountSYP, decimal amountUSD, PricingBillingPeriod billingPeriod)
        {
            _logger.LogInformation("Legacy pricing endpoint UpdatePricing is deprecated. Redirecting to ServiceCatalog.");
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// حذف سعر خدمة (GET تأكيد)
        /// </summary>
        [HttpGet]
        public IActionResult DeletePricing(int id)
        {
            _logger.LogInformation("Legacy pricing endpoint DeletePricing (GET) is deprecated. Redirecting to ServiceCatalog.");
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// حذف سعر خدمة (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePricingConfirm(int id)
        {
            _logger.LogInformation("Legacy pricing endpoint DeletePricingConfirm is deprecated. Redirecting to ServiceCatalog.");
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// حذف جميع أسعار الخدمات (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAllPricingConfirm()
        {
            _logger.LogInformation("Legacy pricing endpoint DeleteAllPricingConfirm is deprecated. Redirecting to ServiceCatalog.");
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// تحميل إحصائيات لوحة التحكم الكاملة
        /// </summary>
        private async Task LoadDashboardStats()
        {
            ViewData["Title"] = "لوحة تحكم مدير النظام";

            Dictionary<int, string> networkNames = await _context.Networks.ToDictionaryAsync(n => n.Id, n => n.Name ?? "");
            Dictionary<int, string> serverNames = await _context.MikroTikServers.ToDictionaryAsync(s => s.Id, s => s.Name ?? "");

            // الشركات = شبكات رئيسية (بدون والد)
            List<Network> companies = await _context.Networks.Where(n => n.ParentNetworkId == null).ToListAsync();
            List<Network> mainNetworks = companies;
            List<Network> subNetworks = await _context.Networks.Where(n => n.ParentNetworkId != null).ToListAsync();

            ViewBag.TotalCompanies = mainNetworks.Count;
            ViewBag.TotalMainNetworks = mainNetworks.Count;
            ViewBag.TotalSubNetworks = subNetworks.Count;

            // العملاء
            List<Client> allClients = await _context.Clients.Include(c => c.Network).Include(c => c.MikroTikServer).Include(c => c.Receiver).ToListAsync();
            ViewBag.TotalClients = allClients.Count;

            // العملاء لكل شركة (الشبكة الرئيسية = الشركة)
            List<StatRowItem> clientCountByCompany = [];
            foreach (Network? c in companies)
            {
                List<int> networkIds = await _context.Networks.Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id).Select(n => n.Id).ToListAsync();
                int count = allClients.Count(cl => cl.NetworkId.HasValue && networkIds.Contains(cl.NetworkId.Value));
                clientCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.ClientCountByCompany = clientCountByCompany;

            // العملاء لكل شبكة
            List<NetworkIdCountRow> clientCountByNetwork = await _context.Clients
                .Where(c => c.NetworkId != null)
                .GroupBy(c => c.NetworkId!.Value)
                .Select(g => new NetworkIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            ViewBag.ClientCountByNetwork = clientCountByNetwork.Select(x => new StatRowItem { Id = x.NetworkId, Name = networkNames.GetValueOrDefault(x.NetworkId, ""), Count = x.Count }).ToList();

            // العملاء لكل سيرفر
            List<ServerIdCountRow> clientCountByServer = await _context.Clients
                .Where(c => c.MikroTikServerId != null)
                .GroupBy(c => c.MikroTikServerId!.Value)
                .Select(g => new ServerIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            ViewBag.ClientCountByServer = clientCountByServer.Select(x => new StatRowItem { Id = x.ServerId, Name = serverNames.GetValueOrDefault(x.ServerId, ""), Count = x.Count }).ToList();

            // العملاء لكل قطاع (مرسل) - عبر المستقبلات التابعة للقطاع
            List<int> sectorIds = await _context.Sectors.Select(s => s.Id).ToListAsync();
            List<StatRowItem> clientCountBySector = [];
            foreach (int sid in sectorIds)
            {
                List<int> receiverIds = await _context.Receivers.Where(r => r.SectorId == sid).Select(r => r.Id).ToListAsync();
                int count = allClients.Count(c => c.ReceiverId.HasValue && receiverIds.Contains(c.ReceiverId.Value));
                Sector? sector = await _context.Sectors.FindAsync(sid);
                clientCountBySector.Add(new StatRowItem { Id = sid, Name = sector?.Name ?? "", Count = count });
            }
            ViewBag.ClientCountBySector = clientCountBySector;

            // العملاء لكل مستقبل
            List<ReceiverIdCountRow> clientCountByReceiver = await _context.Clients
                .Where(c => c.ReceiverId != null)
                .GroupBy(c => c.ReceiverId!.Value)
                .Select(g => new ReceiverIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            Dictionary<int, string> receiverNames = await _context.Receivers.Where(r => clientCountByReceiver.Select(x => x.ReceiverId).Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "");
            ViewBag.ClientCountByReceiver = clientCountByReceiver.Select(x => new StatRowItem { Id = x.ReceiverId, Name = receiverNames.GetValueOrDefault(x.ReceiverId, ""), Count = x.Count }).ToList();

            // القطاعات (المرسلات)
            List<Sector> allSectors = await _context.Sectors.Include(s => s.Network).Include(s => s.MikroTikServer).ToListAsync();
            ViewBag.TotalSectors = allSectors.Count;

            List<StatRowItem> sectorCountByCompany = [];
            foreach (Network? c in companies)
            {
                List<int> networkIds = await _context.Networks.Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id).Select(n => n.Id).ToListAsync();
                int count = allSectors.Count(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value));
                sectorCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.SectorCountByCompany = sectorCountByCompany;

            List<NetworkIdCountRow> sectorCountByNetwork = await _context.Sectors
                .Where(s => s.NetworkId != null)
                .GroupBy(s => s.NetworkId!.Value)
                .Select(g => new NetworkIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            ViewBag.SectorCountByNetwork = sectorCountByNetwork.Select(x => new StatRowItem { Id = x.NetworkId, Name = networkNames.GetValueOrDefault(x.NetworkId, ""), Count = x.Count }).ToList();

            List<ServerIdCountRow> sectorCountByServer = await _context.Sectors
                .GroupBy(s => s.MikroTikServerId)
                .Select(g => new ServerIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            ViewBag.SectorCountByServer = sectorCountByServer.Select(x => new StatRowItem { Id = x.ServerId, Name = serverNames.GetValueOrDefault(x.ServerId, ""), Count = x.Count }).ToList();

            // المستقبلات
            List<Receiver> allReceivers = await _context.Receivers.Include(r => r.Network).Include(r => r.Sector).ToListAsync();
            ViewBag.TotalReceivers = allReceivers.Count;

            List<StatRowItem> receiverCountByCompany = [];
            foreach (Network? c in companies)
            {
                List<int> networkIds = await _context.Networks.Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id).Select(n => n.Id).ToListAsync();
                int count = allReceivers.Count(r => r.NetworkId.HasValue && networkIds.Contains(r.NetworkId.Value));
                receiverCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.ReceiverCountByCompany = receiverCountByCompany;

            List<NetworkIdCountRow> receiverCountByNetwork = await _context.Receivers
                .Where(r => r.NetworkId != null)
                .GroupBy(r => r.NetworkId!.Value)
                .Select(g => new NetworkIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            ViewBag.ReceiverCountByNetwork = receiverCountByNetwork.Select(x => new StatRowItem { Id = x.NetworkId, Name = networkNames.GetValueOrDefault(x.NetworkId, ""), Count = x.Count }).ToList();

            List<ServerIdCountRow> receiverCountByServerGrouped = await _context.Receivers
                .Include(r => r.Sector)
                .Where(r => r.Sector != null)
                .GroupBy(r => r.Sector!.MikroTikServerId)
                .Select(g => new ServerIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            ViewBag.ReceiverCountByServer = receiverCountByServerGrouped.Select(x => new StatRowItem { Id = x.ServerId, Name = serverNames.GetValueOrDefault(x.ServerId, ""), Count = x.Count }).ToList();

            List<SectorIdCountRow> receiverCountBySector = await _context.Receivers
                .GroupBy(r => r.SectorId)
                .Select(g => new SectorIdCountRow(g.Key, g.Count()))
                .ToListAsync();
            Dictionary<int, string> sectorNames = await _context.Sectors.Where(s => receiverCountBySector.Select(x => x.SectorId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name ?? "");
            ViewBag.ReceiverCountBySector = receiverCountBySector.Select(x => new StatRowItem { Id = x.SectorId, Name = sectorNames.GetValueOrDefault(x.SectorId, ""), Count = x.Count }).ToList();

            // السيرفرات
            List<MikroTikServer> allServers = await _context.MikroTikServers.ToListAsync();
            ViewBag.TotalServers = allServers.Count;

            List<StatRowItem> serverCountByCompany = [];
            foreach (Network? c in companies)
            {
                List<int> networkIds = await _context.Networks
                    .Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id)
                    .Select(n => n.Id)
                    .ToListAsync();

                int count = allServers.Count(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value));
                serverCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.ServerCountByCompany = serverCountByCompany;
        }

        /// <summary>
        /// تحميل إحصائيات طلبات الصيانة لكل شركة ونسبة الصيانات للمشتركين
        /// </summary>
        private async Task LoadMaintenanceStats(double slaHours)
        {
            ViewData["Title"] = "طلبات الصيانة - مدير النظام";
            ViewBag.MaintenanceSlaHours = slaHours;

            List<Network> companies = await _context.Networks.Where(n => n.ParentNetworkId == null).ToListAsync();
            List<MaintenanceStatItem> maintenanceStats = [];

            foreach (Network? company in companies)
            {
                List<int> networkIds = await _context.Networks.Where(n => n.ParentNetworkId == company.Id || n.Id == company.Id).Select(n => n.Id).ToListAsync();
                int subscribersCount = await _context.Clients.CountAsync(c => c.NetworkId != null && networkIds.Contains(c.NetworkId.Value));

                IQueryable<MaintenanceRequest> requestsQuery = _context.MaintenanceRequests
                    .Include(m => m.Client)
                    .Where(m => m.Client != null && m.Client.NetworkId != null && networkIds.Contains(m.Client.NetworkId.Value));
                int totalRequests = await requestsQuery.CountAsync();
                int completedRequests = await requestsQuery.CountAsync(m => m.Status == MaintenanceRequestStatus.Completed);

                // متوسط زمن التلبية + نسبة الالتزام بالـ SLA
                List<MaintenanceCompletedTimingRow> completedTimes = await requestsQuery
                    .Where(m => m.Status == MaintenanceRequestStatus.Completed && m.CompletedDate != null)
                    .Select(m => new MaintenanceCompletedTimingRow(m.RequestDate, m.CompletedDate))
                    .ToListAsync();

                List<double> durationsHours = completedTimes
                    .Select(x => (x.CompletedDate!.Value - x.RequestDate).TotalHours)
                    .Where(h => h >= 0)
                    .ToList();

                double? avgFulfillmentHours = durationsHours.Count > 0 ? Math.Round(durationsHours.Average(), 2) : null;
                double? slaCompliancePercent = durationsHours.Count > 0
                    ? Math.Round(durationsHours.Count(h => h <= slaHours) * 100.0 / durationsHours.Count, 2)
                    : null;

                double ratio = subscribersCount > 0 ? (double)completedRequests / subscribersCount * 100.0 : 0;
                maintenanceStats.Add(new MaintenanceStatItem
                {
                    CompanyId = company.Id,
                    CompanyName = company.Name ?? "",
                    TotalRequests = totalRequests,
                    CompletedRequests = completedRequests,
                    SubscribersCount = subscribersCount,
                    RatioPercent = Math.Round(ratio, 2),
                    AvgFulfillmentHours = avgFulfillmentHours,
                    SlaCompliancePercent = slaCompliancePercent
                });
            }

            ViewBag.MaintenanceStats = maintenanceStats;
        }

        /// <summary>
        /// تحميل بيانات نقاط التحصيل (الاسم، العنوان، الجوال)
        /// </summary>
        private async Task LoadCollectionPointsData()
        {
            ViewData["Title"] = "نقاط التحصيل - مدير النظام";

            IList<ApplicationUser> collectionPointUsers = await _userManager.GetUsersInRoleAsync("CollectionPoint");
            List<CollectionPointAccount> accounts = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .ToListAsync();

            List<CollectionPointDisplayItem> points = collectionPointUsers.Select(u => new CollectionPointDisplayItem
            {
                UserId = u.Id,
                Name = u.FullName ?? u.UserName ?? "",
                Address = u.Address ?? "-",
                Mobile = u.PhoneNumber ?? "-"
            }).ToList();

            ViewBag.CollectionPoints = points;
        }

        /// <summary>
        /// طلبات مديري الشركة (اعتماد طلبات التسجيل)
        /// </summary>
        public IActionResult NetworkAdminRequests()
        {
            // Keep SystemAdministrator UX under /systemAdmin/*
            return RedirectToRoute("systemAdmin-joinRequests");
        }

        /// <summary>
        /// الشركات ومدراء الشركة
        /// </summary>
        public IActionResult NetworksAndAdmins()
        {
            // Ensure SystemAdmin stays within its Area UI.
            return RedirectToAction("Index", "Network", new { area = "SystemAdmin" });
        }
    }

    public class MaintenanceStatItem
    {
        public int CompanyId { get; set; }
        public string CompanyName { get; set; } = "";
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int SubscribersCount { get; set; }
        public double RatioPercent { get; set; }
        public double? AvgFulfillmentHours { get; set; }
        public double? SlaCompliancePercent { get; set; }
    }

    public class CollectionPointDisplayItem
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Address { get; set; } = "";
        public string Mobile { get; set; } = "";
    }

    /// <summary>عنصر إحصائية للعرض (شركة/شبكة/سيرفر... + العدد)</summary>
    public class StatRowItem
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }
}
