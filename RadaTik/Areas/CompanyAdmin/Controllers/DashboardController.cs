using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.ViewModels.UI;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class DashboardController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IFeatureAccessService featureAccess,
    ICompanyBusinessSummaryService businessSummaryService,
    IOnboardingChecklistService onboardingChecklistService) : Controller
{
    private sealed record InsightsReceiverRow(int Id, string Name, double Latitude, double Longitude, int Clients);

    private sealed record InsightsSectorRawRow(
        int Id,
        string Name,
        double Lat,
        double Lng,
        double Direction,
        double CoverageAngle,
        double CoverageRange,
        int ReceiverCount,
        int SubscriberCount,
        decimal PackagesTotal);

    private sealed record InsightsSectorOutRow(
        int Id,
        string Name,
        double Lat,
        double Lng,
        double Direction,
        double CoverageAngle,
        double CoverageRange,
        int ReceiverCount,
        int SubscriberCount,
        decimal PackagesTotal,
        string DetailsUrl);

    private sealed record InsightsClientMapRow(int Id, string Name, double Lat, double Lng);

    private sealed record InsightsActivityRow(int Id, string Title, DateTime At);

    private readonly ApplicationDbContext _context = context;
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IFeatureAccessService _featureAccess = featureAccess;
    private readonly ICompanyBusinessSummaryService _businessSummaryService = businessSummaryService;
    private readonly IOnboardingChecklistService _onboardingChecklistService = onboardingChecklistService;

    /// <summary>
    /// لوحة تحكم مدير الشركة (NetworkAdministrator) - إحصائيات الشبكة الحالية.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "لوحة تحكم مدير الشركة";
        ViewData["DashboardType"] = "Admin";
        // Helps CSS match React /app/manager sidebar (hide unrelated sections on desktop).
        // Scoped page hooks (sidebar uses full _SidebarNavSections on all manager pages).
        ViewData["BodyClass"] = "manager-dashboard-page";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل البدء في استخدام النظام";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        // نفس منطق HomeController.AdminDashboard لكن ضمن Area منظمة.
        int totalClients = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value);
        int activeClients = await _context.Clients.CountAsync(c => c.IsActive && c.NetworkId == networkId.Value);
        int inactiveClients = totalClients - activeClients;

        int totalSectors = await _context.Sectors.CountAsync(s => s.NetworkId == networkId.Value);
        int activeSectors = await _context.Sectors.CountAsync(s => s.IsActive && s.NetworkId == networkId.Value);

        int totalReceivers = await _context.Receivers.CountAsync(r => r.NetworkId == networkId.Value);
        int activeReceivers = await _context.Receivers.CountAsync(r => r.IsActive && r.NetworkId == networkId.Value);

        int totalServers = await _context.MikroTikServers.CountAsync(s => s.NetworkId == networkId.Value);
        int activeServers = await _context.MikroTikServers.CountAsync(s => s.IsActive && s.NetworkId == networkId.Value);

        DateTime today = DateTime.Now;
        int expiredSubscriptions = await _context.Clients
            .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue && c.AccountExpirationDate.Value <= today);

        int expiringInWeek = await _context.Clients
            .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value > today
                && c.AccountExpirationDate.Value <= today.AddDays(7));

        List<CompanyAdminDashboardRecentClient> recentClients = await _context.Clients
            .Where(c => c.NetworkId == networkId.Value)
            .OrderByDescending(c => c.CreatedDate)
            .Take(5)
            .Select(c => new CompanyAdminDashboardRecentClient(c.Id, c.Name, c.UserName, c.IsActive, c.CreatedDate))
            .ToListAsync();

        int profileNetworkId = networkId.Value;
        List<CompanyAdminDashboardProfileStat> profileStats = (await _context.Profiles
                .Where(p => p.IsActive && p.NetworkId == profileNetworkId)
                .Select(p => new
                {
                    p.Name,
                    ClientCount = _context.Clients.Count(c => c.ProfileId == p.Id && c.NetworkId == profileNetworkId)
                })
                .OrderByDescending(x => x.ClientCount)
                .Take(5)
                .ToListAsync())
            .Select(x => new CompanyAdminDashboardProfileStat(x.Name, x.ClientCount))
            .ToList();

        DateTime startOfMonth = new(today.Year, today.Month, 1);
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

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = networkId;

        Network? selectedNetwork = await _context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value);
        if (selectedNetwork != null)
        {
            int companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
            bool hasBusinessModule = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Warehouse)
                || await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.MoneyDiary)
                || await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Payroll);
            bool hasRequests = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Requests);
            ViewBag.ShowCompanyBusiness = hasBusinessModule;
            ViewBag.ShowMaintenanceInvoicesSummary = hasRequests;
            if (hasBusinessModule || hasRequests)
            {
                ViewBag.CompanyBusinessSummary = await _businessSummaryService.GetSummaryAsync(companyNetworkId);
            }
        }

        ViewBag.OnboardingChecklist = await _onboardingChecklistService.GetCompanyChecklistAsync(
            user.Id,
            networkId.Value);

        // مسار صريح يتجنب فشل حل موقع العرض إذا تعثر محرك المناطق أو التجميع.
        return View("~/Areas/CompanyAdmin/Views/Dashboard/Index.cshtml");
    }

    /// <summary>
    /// مركز العمليات — مهام يومية: منتهون، طلبات، تجديد، مشترك جديد.
    /// </summary>
    public async Task<IActionResult> Operations()
    {
        ViewData["Title"] = "مركز العمليات";
        ViewData["BodyClass"] = "manager-dashboard-page operations-hub-page";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل البدء في استخدام النظام";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        DateTime today = DateTime.Now;
        int expiredSubscriptions = await _context.Clients
            .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue && c.AccountExpirationDate.Value <= today);
        int expiringInWeek = await _context.Clients
            .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value > today
                && c.AccountExpirationDate.Value <= today.AddDays(7));

        Network? selectedNetwork = await _context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
        var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        int pendingRequests = await _context.MaintenanceRequests.AsNoTracking()
            .CountAsync(r => r.Status == MaintenanceRequestStatus.Pending &&
                             r.Client != null && r.Client.NetworkId == networkId.Value);
        pendingRequests += await _context.SpeedChangeRequests.AsNoTracking()
            .CountAsync(r => r.Status == SpeedChangeRequestStatus.Pending &&
                             r.Client != null && r.Client.NetworkId == networkId.Value);

        int pendingEmployeeApprovals = await _context.NetworkServiceRequests.AsNoTracking()
            .CountAsync(r => companyScope.Contains(r.NetworkId) &&
                             r.Status == NetworkServiceRequestStatus.Pending &&
                             ((r.Notes != null && r.Notes.StartsWith("EMP_REQ:")) ||
                              (r.Notes != null && r.Notes.Contains("SECTOR_CREATE_PENDING:"))));

        int pendingRenewalRequests = await _context.CollectionPointRenewalRequests.AsNoTracking()
            .CountAsync(r => r.NetworkId == networkId.Value && r.Status == CollectionPointRenewalStatus.Pending);

        int pendingClientTopUps = await _context.ClientWalletTopUpRequests.AsNoTracking()
            .CountAsync(r => companyScope.Contains(r.NetworkId) &&
                             r.Status == ClientWalletTopUpRequestStatus.Pending);

        var model = new OperationsHubViewModel
        {
            ExpiredSubscriptions = expiredSubscriptions,
            ExpiringInWeek = expiringInWeek,
            PendingRequests = pendingRequests,
            PendingEmployeeApprovals = pendingEmployeeApprovals,
            PendingRenewalRequests = pendingRenewalRequests,
            PendingClientTopUps = pendingClientTopUps
        };

        return View("~/Areas/CompanyAdmin/Views/Dashboard/Operations.cshtml", model);
    }

    /// <summary>
    /// بيانات المخططات والخريطة للوحة التحكم — شبكة مدير الشركة الحالية (JSON).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> InsightsData()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return Json(new { error = "network_required" });
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(new { error = "network_required" });
        }

        int nid = networkId.Value;
        DateTime today = DateTime.Now;
        DateTime monthEnd = new DateTime(today.Year, today.Month, 1);
        DateTime monthStart = monthEnd.AddMonths(-11);
        DateTime fromDay = today.Date.AddDays(-29);

        List<DateTime> signupDates = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == nid && c.CreatedDate >= monthStart)
            .Select(c => c.CreatedDate)
            .ToListAsync();

        int monthSpan = ((monthEnd.Year - monthStart.Year) * 12) + (monthEnd.Month - monthStart.Month) + 1;
        List<object> monthly = [.. Enumerable.Range(0, monthSpan).Select(i =>
        {
            DateTime d = monthStart.AddMonths(i);
            DateTime next = d.AddMonths(1);
            int users = signupDates.Count(x => x >= d && x < next);
            return (object)new
            {
                M = d.ToString("yyyy/MM", CultureInfo.InvariantCulture),
                Users = users
            };
        })];

        int dailyCount = (today.Date - fromDay).Days + 1;
        List<object> daily = [.. Enumerable.Range(0, dailyCount).Select(i =>
        {
            DateTime day = fromDay.AddDays(i);
            DateTime next = day.AddDays(1);
            int count = signupDates.Count(x => x >= day && x < next);
            return (object)new
            {
                T = day.ToString("dd/MM", CultureInfo.InvariantCulture),
                Count = count
            };
        })];

        List<InsightsReceiverRow> receivers = await _context.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId == nid && r.IsActive)
            .Select(r => new InsightsReceiverRow(
                r.Id,
                r.Name ?? "",
                r.Latitude,
                r.Longitude,
                _context.Clients.Count(c => c.ReceiverId == r.Id && c.NetworkId == nid)))
            .ToListAsync();

        List<InsightsSectorRawRow> sectorsRaw = await _context.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == nid && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new InsightsSectorRawRow(
                s.Id,
                s.Name ?? "",
                s.Latitude,
                s.Longitude,
                s.Direction,
                s.CoverageAngle,
                s.CoverageRange,
                _context.Receivers.Count(r => r.SectorId == s.Id && r.NetworkId == nid && r.IsActive),
                _context.Clients.Count(c =>
                    c.NetworkId == nid && c.IsActive && c.Receiver != null && c.Receiver.SectorId == s.Id),
                _context.Clients
                    .Where(c => c.NetworkId == nid && c.IsActive && c.Receiver != null && c.Receiver.SectorId == s.Id)
                    .Sum(c => c.Profile == null ? 0m : c.Profile.Price * (1 + c.Profile.VATPercentage / 100m))))
            .ToListAsync();

        List<InsightsSectorOutRow> sectors = sectorsRaw
            .Select(s => new InsightsSectorOutRow(
                s.Id,
                s.Name,
                s.Lat,
                s.Lng,
                s.Direction,
                s.CoverageAngle,
                s.CoverageRange,
                s.ReceiverCount,
                s.SubscriberCount,
                s.PackagesTotal,
                Url.Action("Details", "Sector", new { area = "CompanyAdmin", id = s.Id }) ?? string.Empty))
            .ToList();

        List<InsightsClientMapRow> clientsMap = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == nid && c.IsActive &&
                        c.Latitude != null && c.Longitude != null)
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new InsightsClientMapRow(
                c.Id,
                c.Name ?? c.UserName ?? "",
                c.Latitude!.Value,
                c.Longitude!.Value))
            .Take(800)
            .ToListAsync();

        List<InsightsActivityRow> activity = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == nid)
            .OrderByDescending(c => c.CreatedDate)
            .Take(8)
            .Select(c => new InsightsActivityRow(
                c.Id,
                (c.Name ?? c.UserName ?? "عميل") + (c.IsActive ? "" : " (غير نشط)"),
                c.CreatedDate))
            .ToListAsync();

        return Json(new
        {
            monthlySignups = monthly,
            dailySignups = daily,
            receivers,
            sectors,
            clients = clientsMap,
            activity
        });
    }
}
