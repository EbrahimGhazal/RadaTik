using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
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
    /// لوحة تحكم الموظف التابع للشركة (CompanyEmployee).
    /// </summary>
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "لوحة تحكم الموظف";
        ViewData["DashboardType"] = "Employee";

        var today = DateTime.Now;
        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

        if (!networkId.HasValue)
        {
            TempData["Error"] = "لم يتم ربط حسابك بأي شبكة. يرجى التواصل مع مدير الشركة.";
            ViewBag.MaintenancePendingUntilToday = new List<MaintenanceRequest>();
            ViewBag.InstallationPendingUntilToday = new List<Client>();
            ViewBag.MaintenanceScheduledTomorrow = new List<MaintenanceRequest>();
            ViewBag.InstallationScheduledTomorrow = new List<Client>();
            return View();
        }

        var todayDate = today.Date;
        var tomorrowDate = todayDate.AddDays(1);

        // مهام الصيانة غير المنجزة حتى تاريخ اليوم (متأخر + اليوم)
        var maintenancePendingUntilToday = await _context.MaintenanceRequests
            .Include(m => m.Client)
                .ThenInclude(c => c!.Profile)
            .Where(m => m.Client != null
                && m.Client.NetworkId == networkId.Value
                && m.ScheduledVisitDate.HasValue
                && m.ScheduledVisitDate.Value.Date <= todayDate
                && m.Status != MaintenanceRequestStatus.Completed
                && m.Status != MaintenanceRequestStatus.Rejected
                && m.Status != MaintenanceRequestStatus.Cancelled)
            .OrderBy(m => m.ScheduledVisitDate)
            .ToListAsync();

        // مهام التركيب حتى تاريخ اليوم
        var installationPendingUntilToday = await _context.Clients
            .Include(c => c.Profile)
            .Where(c => c.NetworkId == networkId.Value
                && c.CreatedDate.Date <= todayDate)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync();

        // مهام يوم الغد
        var maintenanceScheduledTomorrow = await _context.MaintenanceRequests
            .Include(m => m.Client)
                .ThenInclude(c => c!.Profile)
            .Where(m => m.Client != null
                && m.Client.NetworkId == networkId.Value
                && m.ScheduledVisitDate.HasValue
                && m.ScheduledVisitDate.Value.Date == tomorrowDate
                && m.Status != MaintenanceRequestStatus.Completed
                && m.Status != MaintenanceRequestStatus.Rejected
                && m.Status != MaintenanceRequestStatus.Cancelled)
            .OrderBy(m => m.ScheduledVisitDate)
            .ToListAsync();

        var installationScheduledTomorrow = await _context.Clients
            .Include(c => c.Profile)
            .Where(c => c.NetworkId == networkId.Value
                && c.CreatedDate.Date == tomorrowDate)
            .OrderBy(c => c.CreatedDate)
            .ToListAsync();

        ViewBag.MaintenancePendingUntilToday = maintenancePendingUntilToday;
        ViewBag.InstallationPendingUntilToday = installationPendingUntilToday;
        ViewBag.MaintenanceScheduledTomorrow = maintenanceScheduledTomorrow;
        ViewBag.InstallationScheduledTomorrow = installationScheduledTomorrow;

        return View();
    }

    /// <summary>صفحة محفظة الموظف (تعرض رصيد شبكة الموظف وآخر الحركات).</summary>
    public async Task<IActionResult> Wallet()
    {
        ViewData["Title"] = "محفظة الموظف";

        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = "لم يتم تحديد شبكة لهذا الموظف.";
            return RedirectToAction(nameof(Index));
        }

        var selected = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
        var effectiveId = selected?.ParentNetworkId ?? networkId.Value;
        var effectiveNetwork = effectiveId == networkId.Value
            ? selected
            : await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveId);

        var txs = await _context.NetworkWalletTransactions
            .AsNoTracking()
            .Where(t => t.NetworkId == effectiveId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.NetworkName = effectiveNetwork?.Name ?? "—";
        ViewBag.NetworkBalance = effectiveNetwork?.Balance ?? 0m;
        return View(txs);
    }
}

