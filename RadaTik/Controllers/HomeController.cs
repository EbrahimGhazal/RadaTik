using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Helpers;
using RadaTik.Security;

namespace RadaTik.Controllers
{
    public class HomeController : Controller
    {
        private sealed record AdminDashboardRecentClient(int Id, string? Name, string? UserName, bool IsActive, DateTime CreatedDate);

        private sealed record AdminDashboardProfileStat(string? Name, int ClientCount);

        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            ILogger<HomeController> logger,
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager)
        {
            _logger = logger;
            _context = context;
            _userManager = userManager;
        }

        [AllowAnonymous]
        public async Task<IActionResult> Index()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return Redirect("/radatik");
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Redirect("/radatik");
            }

            IList<string> userRoles = await _userManager.GetRolesAsync(user);

            // تحديد نوع لوحة التحكم بناءً على دور المستخدم
            if (userRoles.Contains(RoleNames.SystemAdministrator))
            {
                // مدير النظام - إعادة التوجيه إلى واجهة SystemAdmin الجديدة
                return RedirectToAction("Index", "SystemAdmin", new { tab = "dashboard" });
            }
            else if (userRoles.Contains(RoleNames.NetworkAdministrator))
            {
                // التحقق من وجود شبكة للمستخدم
                if (!user.NetworkId.HasValue)
                {
                    // إذا لم يكن لديه شبكة، توجيهه لإنشاء شبكة
                    TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل البدء في استخدام النظام";
                    return RedirectToAction("Create", "Network");
                }

                // توجيه إلى Area منظمة لمدير الشركة (URLs واضحة وقابلة للتوسع)
                return RedirectToRoute("networkManager-dashboard");
            }
            else if (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy))
            {
                // توجيه إلى Area منظمة لموظفي الشركة
                return RedirectToRoute("employee-dashboard");
            }
            else if (userRoles.Contains(RoleNames.CollectionPoint))
            {
                return RedirectToRoute("collectionPoint-dashboard");
            }
            else if (userRoles.Contains(RoleNames.Client))
            {
                return RedirectToRoute("clientPortal-dashboard");
            }

            return View();
        }

        private async Task<IActionResult> SystemDashboard()
        {
            ViewData["Title"] = "لوحة تحكم مدير النظام";
            ViewData["DashboardType"] = "System";

            // الشركات (الشبكات الرئيسية) والشبكات الفرعية
            int totalCompanies = await _context.Networks.CountAsync(n => n.ParentNetworkId == null);
            int totalSubNetworks = await _context.Networks.CountAsync(n => n.ParentNetworkId != null);

            // إحصائيات عامة على كامل النظام
            int totalClients = await _context.Clients.CountAsync();
            int activeClients = await _context.Clients.CountAsync(c => c.IsActive);
            int totalServers = await _context.MikroTikServers.CountAsync();
            int totalSectors = await _context.Sectors.CountAsync();
            int totalReceivers = await _context.Receivers.CountAsync();

            // طلبات مديري الشركة
            int pendingCompanyManagers = await _context.JoinRequests.CountAsync(r =>
                r.RequestType == JoinRequestType.NetworkAdministrator &&
                (r.Status == JoinRequestStatus.Pending || r.Status == JoinRequestStatus.UnderReview));

            ViewBag.TotalCompanies = totalCompanies;
            ViewBag.TotalSubNetworks = totalSubNetworks;
            ViewBag.TotalClients = totalClients;
            ViewBag.ActiveClients = activeClients;
            ViewBag.TotalServers = totalServers;
            ViewBag.TotalSectors = totalSectors;
            ViewBag.TotalReceivers = totalReceivers;
            ViewBag.PendingCompanyManagers = pendingCompanyManagers;

            return View("SystemDashboard");
        }

        private async Task<IActionResult> AdminDashboard()
        {
            ViewData["Title"] = "لوحة تحكم مدير الشركة";
            ViewData["DashboardType"] = "Admin";

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !user.NetworkId.HasValue)
            {
                return RedirectToAction("Create", "Network");
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return RedirectToAction("Index", "Network");
            }

            // إحصائيات عامة - م filtradas حسب الشبكة
            int totalClients = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value);
            int activeClients = await _context.Clients.CountAsync(c => c.IsActive && c.NetworkId == networkId.Value);
            int inactiveClients = totalClients - activeClients;

            int totalSectors = await _context.Sectors.CountAsync(s => s.NetworkId == networkId.Value);
            int activeSectors = await _context.Sectors.CountAsync(s => s.IsActive && s.NetworkId == networkId.Value);

            int totalReceivers = await _context.Receivers.CountAsync(r => r.NetworkId == networkId.Value);
            int activeReceivers = await _context.Receivers.CountAsync(r => r.IsActive && r.NetworkId == networkId.Value);

            int totalServers = await _context.MikroTikServers.CountAsync(s => s.NetworkId == networkId.Value);
            int activeServers = await _context.MikroTikServers.CountAsync(s => s.IsActive && s.NetworkId == networkId.Value);

            // الاشتراكات المنتهية
            DateTime today = DateTime.Now;
            int expiredSubscriptions = await _context.Clients
                .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue && c.AccountExpirationDate.Value <= today);

            // الاشتراكات التي ستنتهي خلال 7 أيام
            int expiringInWeek = await _context.Clients
                .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue
                    && c.AccountExpirationDate.Value > today
                    && c.AccountExpirationDate.Value <= today.AddDays(7));

            // آخر العملاء المضافين
            List<AdminDashboardRecentClient> recentClients = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .OrderByDescending(c => c.CreatedDate)
                .Take(5)
                .Select(c => new AdminDashboardRecentClient(c.Id, c.Name, c.UserName, c.IsActive, c.CreatedDate))
                .ToListAsync();

            // إحصائيات الباقات
            int profileNetworkId = networkId.Value;
            List<AdminDashboardProfileStat> profileStats = (await _context.Profiles
                    .Where(p => p.IsActive && p.NetworkId == profileNetworkId)
                    .Select(p => new
                    {
                        p.Name,
                        ClientCount = _context.Clients.Count(c => c.ProfileId == p.Id && c.NetworkId == profileNetworkId)
                    })
                    .OrderByDescending(x => x.ClientCount)
                    .Take(5)
                    .ToListAsync())
                .Select(x => new AdminDashboardProfileStat(x.Name, x.ClientCount))
                .ToList();

            // حساب نسبة التغيير (مثال: العملاء الجدد هذا الشهر)
            DateTime startOfMonth = new DateTime(today.Year, today.Month, 1);
            int newClientsThisMonth = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value && c.CreatedDate >= startOfMonth);

            DateTime startOfLastMonth = startOfMonth.AddMonths(-1);
            int newClientsLastMonth = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value && c.CreatedDate >= startOfLastMonth && c.CreatedDate < startOfMonth);

            double clientsChangePercent = newClientsLastMonth > 0
                ? Math.Round(((double)(newClientsThisMonth - newClientsLastMonth) / newClientsLastMonth) * 100, 1)
                : 0;

            ViewBag.TotalClients = totalClients;
            ViewBag.ActiveClients = activeClients;
            ViewBag.InactiveClients = inactiveClients;
            ViewBag.TotalSectors = totalSectors;
            ViewBag.ActiveSectors = activeSectors;
            ViewBag.TotalReceivers = totalReceivers;
            ViewBag.ActiveReceivers = activeReceivers;
            ViewBag.TotalServers = totalServers;
            ViewBag.ActiveServers = activeServers;
            ViewBag.ExpiredSubscriptions = expiredSubscriptions;
            ViewBag.ExpiringInWeek = expiringInWeek;
            ViewBag.RecentClients = recentClients;
            ViewBag.ProfileStats = profileStats;
            ViewBag.NewClientsThisMonth = newClientsThisMonth;
            ViewBag.ClientsChangePercent = clientsChangePercent;

            return View("AdminDashboard");
        }

        private async Task<IActionResult> EmployeeDashboard()
        {
            ViewData["Title"] = "لوحة تحكم الموظف";
            ViewData["DashboardType"] = "Employee";

            DateTime today = DateTime.Now;
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حسابك بأي شبكة. يرجى التواصل مع مدير الشركة.";
                ViewBag.TotalClients = 0;
                ViewBag.ActiveClients = 0;
                ViewBag.ExpiredSubscriptions = 0;
                ViewBag.ExpiringInWeek = 0;
                ViewBag.ExpiringClients = new List<Client>();
                ViewBag.TotalSectors = 0;
                ViewBag.TotalReceivers = 0;
                return View("EmployeeDashboard");
            }

            // إحصائيات العملاء
            int totalClients = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value);
            int activeClients = await _context.Clients.CountAsync(c => c.IsActive && c.NetworkId == networkId.Value);

            // الاشتراكات المنتهية
            int expiredSubscriptions = await _context.Clients
                .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue && c.AccountExpirationDate.Value <= today);

            // الاشتراكات التي ستنتهي خلال 7 أيام
            int expiringInWeek = await _context.Clients
                .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue
                    && c.AccountExpirationDate.Value > today
                    && c.AccountExpirationDate.Value <= today.AddDays(7));

            // العملاء الذين تنتهي اشتراكاتهم قريباً
            List<Client> expiringClients = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue
                    && c.AccountExpirationDate.Value > today
                    && c.AccountExpirationDate.Value <= today.AddDays(7))
                .Include(c => c.Profile)
                .OrderBy(c => c.AccountExpirationDate)
                .Take(10)
                .ToListAsync();

            // إحصائيات القطاعات والمستقبلات
            int totalSectors = await _context.Sectors.CountAsync(s => s.NetworkId == networkId.Value);
            int totalReceivers = await _context.Receivers.CountAsync(r => r.NetworkId == networkId.Value);

            ViewBag.TotalClients = totalClients;
            ViewBag.ActiveClients = activeClients;
            ViewBag.ExpiredSubscriptions = expiredSubscriptions;
            ViewBag.ExpiringInWeek = expiringInWeek;
            ViewBag.ExpiringClients = expiringClients;
            ViewBag.TotalSectors = totalSectors;
            ViewBag.TotalReceivers = totalReceivers;

            return View("EmployeeDashboard");
        }

        private async Task<IActionResult> ClientDashboard(ApplicationUser user)
        {
            ViewData["Title"] = "لوحة تحكم العميل";
            ViewData["DashboardType"] = "Client";

            if (user.ClientId == null)
            {
                ViewBag.NoSubscription = true;
                return View("ClientDashboard");
            }

            Client? client = await _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Receiver)
                    .ThenInclude(r => r!.Sector)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == user.ClientId);

            if (client == null)
            {
                ViewBag.NoSubscription = true;
                return View("ClientDashboard");
            }

            // حساب أيام الاشتراك المتبقية
            int daysRemaining = 0;
            bool isExpired = false;
            if (client.AccountExpirationDate.HasValue)
            {
                TimeSpan remaining = client.AccountExpirationDate.Value - DateTime.Now;
                daysRemaining = (int)remaining.TotalDays;
                isExpired = daysRemaining < 0;
            }

            // حساب نسبة استهلاك الوقت
            double usagePercent = 0;
            if (client.AccountExpirationDate.HasValue && client.CreatedDate != default)
            {
                double totalDays = (client.AccountExpirationDate.Value - client.CreatedDate).TotalDays;
                double usedDays = (DateTime.Now - client.CreatedDate).TotalDays;
                usagePercent = totalDays > 0 ? Math.Round((usedDays / totalDays) * 100, 1) : 0;
                usagePercent = Math.Min(100, Math.Max(0, usagePercent));
            }

            ViewBag.Client = client;
            ViewBag.DaysRemaining = daysRemaining;
            ViewBag.IsExpired = isExpired;
            ViewBag.UsagePercent = usagePercent;
            ViewBag.NoSubscription = false;

            return View("ClientDashboard");
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
