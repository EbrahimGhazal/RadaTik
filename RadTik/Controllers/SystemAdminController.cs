using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Controllers
{
    /// <summary>
    /// لوحة تحكم مدير النظام - إحصائيات، طلبات الصيانة، نقاط التحصيل
    /// </summary>
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class SystemAdminController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<SystemAdminController> _logger;
        private readonly IConfiguration _configuration;

        public SystemAdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<SystemAdminController> logger,
            IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// الصفحة الرئيسية - تبويبات: لوحة التحكم، طلبات الصيانة، نقاط التحصيل
        /// </summary>
        public async Task<IActionResult> Index(string tab = "dashboard", double? slaHours = null)
        {
            var activeTab = string.IsNullOrEmpty(tab) ? "dashboard" : tab.ToLowerInvariant();
            ViewData["ActiveTab"] = activeTab;

            if (activeTab == "dashboard")
            {
                await LoadDashboardStats();
                return View("Index");
            }
            if (activeTab == "maintenance")
            {
                var effectiveSlaHours = NormalizeSlaHours(
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
                TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
                return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
            }

            await LoadDashboardStats();
            return View("Index");
        }

        private double ReadMaintenanceSlaHoursFromConfig(double defaultValue)
        {
            var v = _configuration.GetValue<double?>("Maintenance:SlaHours");
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
        public IActionResult CreatePricing()
        {
            TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// إضافة سعر خدمة (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreatePricing(PricingItemType itemType, decimal amountSYP, decimal amountUSD, PricingBillingPeriod billingPeriod)
        {
            _logger.LogInformation("Legacy pricing endpoint CreatePricing is deprecated. Redirecting to ServiceCatalog in trial mode.");
            TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// تحديث سعر خدمة (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdatePricing(int id, PricingItemType itemType, decimal amountSYP, decimal amountUSD, PricingBillingPeriod billingPeriod)
        {
            _logger.LogInformation("Legacy pricing endpoint UpdatePricing is deprecated. Redirecting to ServiceCatalog in trial mode.");
            TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// حذف سعر خدمة (GET تأكيد)
        /// </summary>
        [HttpGet]
        public IActionResult DeletePricing(int id)
        {
            _logger.LogInformation("Legacy pricing endpoint DeletePricing (GET) is deprecated. Redirecting to ServiceCatalog in trial mode.");
            TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// حذف سعر خدمة (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeletePricingConfirm(int id)
        {
            _logger.LogInformation("Legacy pricing endpoint DeletePricingConfirm is deprecated. Redirecting to ServiceCatalog in trial mode.");
            TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// حذف جميع أسعار الخدمات (POST)
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteAllPricingConfirm()
        {
            _logger.LogInformation("Legacy pricing endpoint DeleteAllPricingConfirm is deprecated. Redirecting to ServiceCatalog in trial mode.");
            TempData["PricingWarning"] = "تم تعطيل إدارة التسعير في المرحلة التجريبية. جميع الخدمات مفعّلة مجاناً.";
            return RedirectToAction("Index", "ServiceCatalog", new { area = "SystemAdmin" });
        }

        /// <summary>
        /// تحميل إحصائيات لوحة التحكم الكاملة
        /// </summary>
        private async Task LoadDashboardStats()
        {
            ViewData["Title"] = "لوحة تحكم مدير النظام";

            var networkNames = await _context.Networks.ToDictionaryAsync(n => n.Id, n => n.Name ?? "");
            var serverNames = await _context.MikroTikServers.ToDictionaryAsync(s => s.Id, s => s.Name ?? "");

            // الشركات = شبكات رئيسية (بدون والد)
            var companies = await _context.Networks.Where(n => n.ParentNetworkId == null).ToListAsync();
            var mainNetworks = companies;
            var subNetworks = await _context.Networks.Where(n => n.ParentNetworkId != null).ToListAsync();

            ViewBag.TotalCompanies = mainNetworks.Count;
            ViewBag.TotalMainNetworks = mainNetworks.Count;
            ViewBag.TotalSubNetworks = subNetworks.Count;

            // العملاء
            var allClients = await _context.Clients.Include(c => c.Network).Include(c => c.MikroTikServer).Include(c => c.Receiver).ToListAsync();
            ViewBag.TotalClients = allClients.Count;

            // العملاء لكل شركة (الشبكة الرئيسية = الشركة)
            var clientCountByCompany = new List<StatRowItem>();
            foreach (var c in companies)
            {
                var networkIds = await _context.Networks.Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id).Select(n => n.Id).ToListAsync();
                var count = allClients.Count(cl => cl.NetworkId.HasValue && networkIds.Contains(cl.NetworkId.Value));
                clientCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.ClientCountByCompany = clientCountByCompany;

            // العملاء لكل شبكة
            var clientCountByNetwork = await _context.Clients
                .Where(c => c.NetworkId != null)
                .GroupBy(c => c.NetworkId!.Value)
                .Select(g => new { NetworkId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.ClientCountByNetwork = clientCountByNetwork.Select(x => new StatRowItem { Id = x.NetworkId, Name = networkNames.GetValueOrDefault(x.NetworkId, ""), Count = x.Count }).ToList();

            // العملاء لكل سيرفر
            var clientCountByServer = await _context.Clients
                .Where(c => c.MikroTikServerId != null)
                .GroupBy(c => c.MikroTikServerId!.Value)
                .Select(g => new { ServerId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.ClientCountByServer = clientCountByServer.Select(x => new StatRowItem { Id = x.ServerId, Name = serverNames.GetValueOrDefault(x.ServerId, ""), Count = x.Count }).ToList();

            // العملاء لكل قطاع (مرسل) - عبر المستقبلات التابعة للقطاع
            var sectorIds = await _context.Sectors.Select(s => s.Id).ToListAsync();
            var clientCountBySector = new List<StatRowItem>();
            foreach (var sid in sectorIds)
            {
                var receiverIds = await _context.Receivers.Where(r => r.SectorId == sid).Select(r => r.Id).ToListAsync();
                var count = allClients.Count(c => c.ReceiverId.HasValue && receiverIds.Contains(c.ReceiverId.Value));
                var sector = await _context.Sectors.FindAsync(sid);
                clientCountBySector.Add(new StatRowItem { Id = sid, Name = sector?.Name ?? "", Count = count });
            }
            ViewBag.ClientCountBySector = clientCountBySector;

            // العملاء لكل مستقبل
            var clientCountByReceiver = await _context.Clients
                .Where(c => c.ReceiverId != null)
                .GroupBy(c => c.ReceiverId!.Value)
                .Select(g => new { ReceiverId = g.Key, Count = g.Count() })
                .ToListAsync();
            var receiverNames = await _context.Receivers.Where(r => clientCountByReceiver.Select(x => x.ReceiverId).Contains(r.Id)).ToDictionaryAsync(r => r.Id, r => r.Name ?? "");
            ViewBag.ClientCountByReceiver = clientCountByReceiver.Select(x => new StatRowItem { Id = x.ReceiverId, Name = receiverNames.GetValueOrDefault(x.ReceiverId, ""), Count = x.Count }).ToList();

            // القطاعات (المرسلات)
            var allSectors = await _context.Sectors.Include(s => s.Network).Include(s => s.MikroTikServer).ToListAsync();
            ViewBag.TotalSectors = allSectors.Count;

            var sectorCountByCompany = new List<StatRowItem>();
            foreach (var c in companies)
            {
                var networkIds = await _context.Networks.Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id).Select(n => n.Id).ToListAsync();
                var count = allSectors.Count(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value));
                sectorCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.SectorCountByCompany = sectorCountByCompany;

            var sectorCountByNetwork = await _context.Sectors
                .Where(s => s.NetworkId != null)
                .GroupBy(s => s.NetworkId!.Value)
                .Select(g => new { NetworkId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.SectorCountByNetwork = sectorCountByNetwork.Select(x => new StatRowItem { Id = x.NetworkId, Name = networkNames.GetValueOrDefault(x.NetworkId, ""), Count = x.Count }).ToList();

            var sectorCountByServer = await _context.Sectors
                .GroupBy(s => s.MikroTikServerId)
                .Select(g => new { ServerId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.SectorCountByServer = sectorCountByServer.Select(x => new StatRowItem { Id = x.ServerId, Name = serverNames.GetValueOrDefault(x.ServerId, ""), Count = x.Count }).ToList();

            // المستقبلات
            var allReceivers = await _context.Receivers.Include(r => r.Network).Include(r => r.Sector).ToListAsync();
            ViewBag.TotalReceivers = allReceivers.Count;

            var receiverCountByCompany = new List<StatRowItem>();
            foreach (var c in companies)
            {
                var networkIds = await _context.Networks.Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id).Select(n => n.Id).ToListAsync();
                var count = allReceivers.Count(r => r.NetworkId.HasValue && networkIds.Contains(r.NetworkId.Value));
                receiverCountByCompany.Add(new StatRowItem { Id = c.Id, Name = c.Name ?? "", Count = count });
            }
            ViewBag.ReceiverCountByCompany = receiverCountByCompany;

            var receiverCountByNetwork = await _context.Receivers
                .Where(r => r.NetworkId != null)
                .GroupBy(r => r.NetworkId!.Value)
                .Select(g => new { NetworkId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.ReceiverCountByNetwork = receiverCountByNetwork.Select(x => new StatRowItem { Id = x.NetworkId, Name = networkNames.GetValueOrDefault(x.NetworkId, ""), Count = x.Count }).ToList();

            var receiverCountByServerGrouped = await _context.Receivers
                .Include(r => r.Sector)
                .Where(r => r.Sector != null)
                .GroupBy(r => r.Sector!.MikroTikServerId)
                .Select(g => new { ServerId = g.Key, Count = g.Count() })
                .ToListAsync();
            ViewBag.ReceiverCountByServer = receiverCountByServerGrouped.Select(x => new StatRowItem { Id = x.ServerId, Name = serverNames.GetValueOrDefault(x.ServerId, ""), Count = x.Count }).ToList();

            var receiverCountBySector = await _context.Receivers
                .GroupBy(r => r.SectorId)
                .Select(g => new { SectorId = g.Key, Count = g.Count() })
                .ToListAsync();
            var sectorNames = await _context.Sectors.Where(s => receiverCountBySector.Select(x => x.SectorId).Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name ?? "");
            ViewBag.ReceiverCountBySector = receiverCountBySector.Select(x => new StatRowItem { Id = x.SectorId, Name = sectorNames.GetValueOrDefault(x.SectorId, ""), Count = x.Count }).ToList();

            // السيرفرات
            var allServers = await _context.MikroTikServers.ToListAsync();
            ViewBag.TotalServers = allServers.Count;

            var serverCountByCompany = new List<StatRowItem>();
            foreach (var c in companies)
            {
                var networkIds = await _context.Networks
                    .Where(n => n.ParentNetworkId == c.Id || n.Id == c.Id)
                    .Select(n => n.Id)
                    .ToListAsync();

                var count = allServers.Count(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value));
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

            var companies = await _context.Networks.Where(n => n.ParentNetworkId == null).ToListAsync();
            var maintenanceStats = new List<MaintenanceStatItem>();

            foreach (var company in companies)
            {
                var networkIds = await _context.Networks.Where(n => n.ParentNetworkId == company.Id || n.Id == company.Id).Select(n => n.Id).ToListAsync();
                var subscribersCount = await _context.Clients.CountAsync(c => c.NetworkId != null && networkIds.Contains(c.NetworkId.Value));

                var requestsQuery = _context.MaintenanceRequests
                    .Include(m => m.Client)
                    .Where(m => m.Client != null && m.Client.NetworkId != null && networkIds.Contains(m.Client.NetworkId.Value));
                var totalRequests = await requestsQuery.CountAsync();
                var completedRequests = await requestsQuery.CountAsync(m => m.Status == MaintenanceRequestStatus.Completed);

                // متوسط زمن التلبية + نسبة الالتزام بالـ SLA
                var completedTimes = await requestsQuery
                    .Where(m => m.Status == MaintenanceRequestStatus.Completed && m.CompletedDate != null)
                    .Select(m => new { m.RequestDate, m.CompletedDate })
                    .ToListAsync();

                var durationsHours = completedTimes
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

            var collectionPointUsers = await _userManager.GetUsersInRoleAsync("CollectionPoint");
            var accounts = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .ToListAsync();

            var points = collectionPointUsers.Select(u => new CollectionPointDisplayItem
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
