using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// لوحة تحكم مدير الشركة (NetworkAdministrator) - إحصائيات الشبكة الحالية.
    /// </summary>
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "لوحة تحكم مدير الشركة";
        ViewData["DashboardType"] = "Admin";
        // Helps CSS match React /app/manager sidebar (hide unrelated sections on desktop).
        ViewData["BodyClass"] = "manager-dashboard-page";

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل البدء في استخدام النظام";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        // نفس منطق HomeController.AdminDashboard لكن ضمن Area منظمة.
        var totalClients = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value);
        var activeClients = await _context.Clients.CountAsync(c => c.IsActive && c.NetworkId == networkId.Value);
        var inactiveClients = totalClients - activeClients;

        var totalSectors = await _context.Sectors.CountAsync(s => s.NetworkId == networkId.Value);
        var activeSectors = await _context.Sectors.CountAsync(s => s.IsActive && s.NetworkId == networkId.Value);

        var totalReceivers = await _context.Receivers.CountAsync(r => r.NetworkId == networkId.Value);
        var activeReceivers = await _context.Receivers.CountAsync(r => r.IsActive && r.NetworkId == networkId.Value);

        var totalServers = await _context.MikroTikServers.CountAsync(s => s.NetworkId == networkId.Value);
        var activeServers = await _context.MikroTikServers.CountAsync(s => s.IsActive && s.NetworkId == networkId.Value);

        var today = DateTime.Now;
        var expiredSubscriptions = await _context.Clients
            .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue && c.AccountExpirationDate.Value <= today);

        var expiringInWeek = await _context.Clients
            .CountAsync(c => c.NetworkId == networkId.Value && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value > today
                && c.AccountExpirationDate.Value <= today.AddDays(7));

        var recentClients = await _context.Clients
            .Where(c => c.NetworkId == networkId.Value)
            .OrderByDescending(c => c.CreatedDate)
            .Take(5)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.UserName,
                c.IsActive,
                c.CreatedDate
            })
            .ToListAsync();

        var profileStats = await _context.Profiles
            .Where(p => p.IsActive && p.NetworkId == networkId.Value)
            .Select(p => new
            {
                p.Name,
                ClientCount = _context.Clients.Count(c => c.ProfileId == p.Id && c.NetworkId == networkId.Value)
            })
            .OrderByDescending(p => p.ClientCount)
            .Take(5)
            .ToListAsync();

        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var newClientsThisMonth = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value && c.CreatedDate >= startOfMonth);

        var startOfLastMonth = startOfMonth.AddMonths(-1);
        var newClientsLastMonth = await _context.Clients.CountAsync(c => c.NetworkId == networkId.Value && c.CreatedDate >= startOfLastMonth && c.CreatedDate < startOfMonth);

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

        // مسار صريح يتجنب فشل حل موقع العرض إذا تعثر محرك المناطق أو التجميع.
        return View("~/Areas/CompanyAdmin/Views/Dashboard/Index.cshtml");
    }

    /// <summary>
    /// بيانات المخططات والخريطة للوحة التحكم — شبكة مدير الشركة الحالية (JSON).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> InsightsData()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return Json(new { error = "network_required" });
        }

        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(new { error = "network_required" });
        }

        var nid = networkId.Value;
        var today = DateTime.Now;
        var monthStart = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
        var fromDay = today.Date.AddDays(-29);

        var signupDates = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == nid && c.CreatedDate >= monthStart)
            .Select(c => c.CreatedDate)
            .ToListAsync();

        var monthly = new List<object>();
        for (var d = monthStart; d <= new DateTime(today.Year, today.Month, 1); d = d.AddMonths(1))
        {
            var next = d.AddMonths(1);
            var users = signupDates.Count(x => x >= d && x < next);
            monthly.Add(new
            {
                m = d.ToString("yyyy/MM", CultureInfo.InvariantCulture),
                users
            });
        }

        var daily = new List<object>();
        for (var day = fromDay; day <= today.Date; day = day.AddDays(1))
        {
            var next = day.AddDays(1);
            var count = signupDates.Count(x => x >= day && x < next);
            daily.Add(new
            {
                t = day.ToString("dd/MM", CultureInfo.InvariantCulture),
                count
            });
        }

        var receivers = await _context.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId == nid && r.IsActive)
            .Select(r => new
            {
                r.Id,
                name = r.Name ?? "",
                r.Latitude,
                r.Longitude,
                clients = _context.Clients.Count(c => c.ReceiverId == r.Id && c.NetworkId == nid)
            })
            .ToListAsync();

        var sectorsRaw = await _context.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == nid && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                name = s.Name ?? "",
                lat = s.Latitude,
                lng = s.Longitude,
                direction = s.Direction,
                coverageAngle = s.CoverageAngle,
                coverageRange = s.CoverageRange,
                receiverCount = _context.Receivers.Count(r => r.SectorId == s.Id && r.NetworkId == nid && r.IsActive),
                subscriberCount = _context.Clients.Count(c =>
                    c.NetworkId == nid && c.IsActive && c.Receiver != null && c.Receiver.SectorId == s.Id),
                packagesTotal = _context.Clients
                    .Where(c => c.NetworkId == nid && c.IsActive && c.Receiver != null && c.Receiver.SectorId == s.Id)
                    .Sum(c => c.Profile == null ? 0m : c.Profile.Price * (1 + c.Profile.VATPercentage / 100m))
            })
            .ToListAsync();

        var sectors = sectorsRaw.Select(s => new
        {
            s.Id,
            s.name,
            s.lat,
            s.lng,
            s.direction,
            s.coverageAngle,
            s.coverageRange,
            s.receiverCount,
            s.subscriberCount,
            s.packagesTotal,
            detailsUrl = Url.Action("Details", "Sector", new { area = "CompanyAdmin", id = s.Id }) ?? string.Empty
        }).ToList();

        var clientsMap = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == nid && c.IsActive &&
                        c.Latitude != null && c.Longitude != null)
            .OrderByDescending(c => c.CreatedDate)
            .Select(c => new
            {
                c.Id,
                name = c.Name ?? c.UserName ?? "",
                lat = c.Latitude!.Value,
                lng = c.Longitude!.Value
            })
            .Take(800)
            .ToListAsync();

        var activity = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == nid)
            .OrderByDescending(c => c.CreatedDate)
            .Take(8)
            .Select(c => new
            {
                id = c.Id,
                title = (c.Name ?? c.UserName ?? "عميل") + (c.IsActive ? "" : " (غير نشط)"),
                at = c.CreatedDate
            })
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

