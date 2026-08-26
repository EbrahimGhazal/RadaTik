using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.Clients;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _permissionService;
    private readonly IEmployeeDashboardQueryService _dashboardQuery;

    public DashboardController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IPermissionService permissionService,
        IEmployeeDashboardQueryService dashboardQuery)
    {
        _context = context;
        _userManager = userManager;
        _permissionService = permissionService;
        _dashboardQuery = dashboardQuery;
    }

    /// <summary>
    /// لوحة تحكم الموظف التابع للشركة (CompanyEmployee).
    /// </summary>
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "لوحة تحكم الموظف";
        ViewData["DashboardType"] = "Employee";

        DateTime today = DateTime.Now;
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

        if (!networkId.HasValue)
        {
            TempData["Error"] = "لم يتم ربط حسابك بأي شبكة. يرجى التواصل مع مدير الشركة.";
            SetEmptyDashboardViewBags();
            return View();
        }

        HashSet<string> permissionKeys = await _permissionService.GetUserPermissionKeysAsync(user!.Id);
        EmployeeDepartment department = user.EmployeeDepartment;
        EmployeeDashboardFocus dashboardFocus = EmployeeDepartmentTemplates.ResolveDashboardFocus(department, permissionKeys);

        ViewBag.EmployeeDepartment = department;
        ViewBag.EmployeeDepartmentName = EmployeeDepartmentTemplates.GetDisplayName(department);
        ViewBag.DashboardFocus = dashboardFocus;

        DateTime todayDate = today.Date;
        DateTime tomorrowDate = todayDate.AddDays(1);
        DateTime expiringThreshold = todayDate.AddDays(3);

        List<MaintenanceRequest> maintenancePendingUntilToday = await _context.MaintenanceRequests
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

        List<Client> installationPendingUntilToday = await _dashboardQuery
            .GetPendingInstallationsUntilAsync(networkId.Value, todayDate);

        List<MaintenanceRequest> maintenanceScheduledTomorrow = await _context.MaintenanceRequests
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

        List<Client> installationScheduledTomorrow = await _dashboardQuery
            .GetPendingInstallationsOnDateAsync(networkId.Value, tomorrowDate);

        ViewBag.MaintenancePendingUntilToday = maintenancePendingUntilToday;
        ViewBag.InstallationPendingUntilToday = installationPendingUntilToday;
        ViewBag.MaintenanceScheduledTomorrow = maintenanceScheduledTomorrow;
        ViewBag.InstallationScheduledTomorrow = installationScheduledTomorrow;

        ViewBag.ClientsExpiringIn3Days = await _context.Clients
            .AsNoTracking()
            .CountAsync(c => c.NetworkId == networkId.Value
                && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value.Date >= todayDate
                && c.AccountExpirationDate.Value.Date <= expiringThreshold);

        ViewBag.ClientsExpired = await _context.Clients
            .AsNoTracking()
            .CountAsync(c => c.NetworkId == networkId.Value
                && c.AccountExpirationDate.HasValue
                && c.AccountExpirationDate.Value.Date < todayDate);

        ViewBag.PendingMaintenanceRequests = await _context.MaintenanceRequests
            .AsNoTracking()
            .CountAsync(m => m.Client != null
                && m.Client.NetworkId == networkId.Value
                && m.Status == MaintenanceRequestStatus.Pending);

        ViewBag.PendingSpeedChangeRequests = await _context.SpeedChangeRequests
            .AsNoTracking()
            .CountAsync(r => r.Client != null
                && r.Client.NetworkId == networkId.Value
                && r.Status == SpeedChangeRequestStatus.Pending);

        ViewBag.ActiveSectorsCount = await _context.Sectors
            .AsNoTracking()
            .CountAsync(s => s.NetworkId == networkId.Value && s.IsActive);

        ViewBag.ActiveReceiversCount = await _context.Receivers
            .AsNoTracking()
            .CountAsync(r => r.NetworkId == networkId.Value && r.IsActive);

        ViewBag.TotalClientsCount = await _context.Clients
            .AsNoTracking()
            .CountAsync(c => c.NetworkId == networkId.Value);

        return View();
    }

    /// <summary>محفظة الموظف — راتب ومستحقات (وليس محفظة الشركة).</summary>
    public IActionResult Wallet()
    {
        return RedirectToAction("Index", "MyPayroll", new { area = "CompanyEmployee" });
    }

    private void SetEmptyDashboardViewBags()
    {
        ViewBag.MaintenancePendingUntilToday = new List<MaintenanceRequest>();
        ViewBag.InstallationPendingUntilToday = new List<Client>();
        ViewBag.MaintenanceScheduledTomorrow = new List<MaintenanceRequest>();
        ViewBag.InstallationScheduledTomorrow = new List<Client>();
        ViewBag.EmployeeDepartment = EmployeeDepartment.None;
        ViewBag.EmployeeDepartmentName = EmployeeDepartmentTemplates.GetDisplayName(EmployeeDepartment.None);
        ViewBag.DashboardFocus = EmployeeDashboardFocus.Balanced;
        ViewBag.ClientsExpiringIn3Days = 0;
        ViewBag.ClientsExpired = 0;
        ViewBag.PendingMaintenanceRequests = 0;
        ViewBag.PendingSpeedChangeRequests = 0;
        ViewBag.ActiveSectorsCount = 0;
        ViewBag.ActiveReceiversCount = 0;
        ViewBag.TotalClientsCount = 0;
    }
}
