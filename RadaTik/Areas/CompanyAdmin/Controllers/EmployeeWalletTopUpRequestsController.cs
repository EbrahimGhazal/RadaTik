using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Payroll)]
public class EmployeeWalletTopUpRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmployeeWalletTopUpService _topUpService;

    public EmployeeWalletTopUpRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        EmployeeWalletTopUpService topUpService)
    {
        _context = context;
        _userManager = userManager;
        _topUpService = topUpService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EmployeeWalletTopUpRequestStatus? status = null)
    {
        ViewData["Title"] = "طلبات تغذية محافظ الموظفين";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        Network? network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
        int companyNetworkId = network?.ParentNetworkId ?? networkId.Value;

        IQueryable<EmployeeWalletTopUpRequest> query = _context.EmployeeWalletTopUpRequests
            .AsNoTracking()
            .Include(r => r.PayrollEmployee)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ProcessedByUser)
            .Where(r => r.CompanyNetworkId == companyNetworkId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        List<EmployeeWalletTopUpRequest> items = await query
            .OrderByDescending(r => r.RequestedAt)
            .Take(500)
            .ToListAsync();

        List<PayrollEmployee> employees = await _context.PayrollEmployees
            .AsNoTracking()
            .Where(e => e.CompanyNetworkId == companyNetworkId && e.IsActive)
            .OrderBy(e => e.FullName)
            .ToListAsync();

        ViewBag.Items = items;
        ViewBag.Employees = employees;
        ViewBag.SelectedStatus = status;
        ViewBag.PendingCount = await _context.EmployeeWalletTopUpRequests.CountAsync(r =>
            r.CompanyNetworkId == companyNetworkId &&
            r.Status == EmployeeWalletTopUpRequestStatus.Pending);
        ViewBag.CompanyName = network?.Name ?? "";
        ViewBag.CompanyWalletBalance = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId)
            .Select(n => n.Balance)
            .FirstOrDefaultAsync();

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? adminNotes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(user);
        if (companyNetworkId <= 0)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        EmployeeWalletTopUpOutcome result = await _topUpService.ApproveRequestAsync(
            id, companyNetworkId, user.Id, adminNotes);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? adminNotes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(user);
        if (companyNetworkId <= 0)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        EmployeeWalletTopUpOutcome result = await _topUpService.RejectRequestAsync(
            id, companyNetworkId, user.Id, adminNotes);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DirectTopUp(int payrollEmployeeId, decimal amount, string? notes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(user);
        if (companyNetworkId <= 0)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        PayrollEmployee? employee = await _context.PayrollEmployees
            .FirstOrDefaultAsync(e => e.Id == payrollEmployeeId && e.CompanyNetworkId == companyNetworkId);
        if (employee == null)
        {
            TempData["Error"] = "الموظف غير موجود.";
            return RedirectToAction(nameof(Index));
        }

        EmployeeWalletTopUpOutcome result = await _topUpService.DirectTopUpAsync(
            employee, companyNetworkId, user.Id, amount, notes);

        TempData[result.Success ? "Success" : "Error"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(ApplicationUser user)
    {
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return 0;
        }

        Network? network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
        return network?.ParentNetworkId ?? networkId.Value;
    }
}
