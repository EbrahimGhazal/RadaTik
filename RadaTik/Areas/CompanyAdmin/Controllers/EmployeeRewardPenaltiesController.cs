using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
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
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Erp)]
public class EmployeeRewardPenaltiesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly EmployeeRewardPenaltyService _rewardPenaltyService;
    private readonly IErpNotificationService _erpNotifications;

    public EmployeeRewardPenaltiesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        EmployeeRewardPenaltyService rewardPenaltyService,
        IErpNotificationService erpNotifications)
    {
        _context = context;
        _userManager = userManager;
        _rewardPenaltyService = rewardPenaltyService;
        _erpNotifications = erpNotifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index(EmployeeRewardPenaltyStatus? status)
    {
        ViewData["Title"] = "المكافآت والعقوبات";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        IQueryable<EmployeeRewardPenalty> query = _context.EmployeeRewardPenalties.AsNoTracking()
            .Include(r => r.PayrollEmployee)
            .Include(r => r.CreatedByUser)
            .Where(r => r.CompanyNetworkId == scope.CompanyNetworkId);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        ViewBag.StatusFilter = status;
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(await query.OrderByDescending(r => r.CreatedAt).Take(200).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "إضافة مكافأة أو عقوبة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        await PopulatePayrollEmployeesAsync(scope.CompanyNetworkId);
        return View(new EmployeeRewardPenalty
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            Currency = PricingCurrency.SYP_New,
            Type = EmployeeRewardPenaltyType.Reward,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EmployeeRewardPenalty model)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        model.CompanyNetworkId = scope.CompanyNetworkId;
        model.Reason = model.Reason?.Trim() ?? string.Empty;
        if (model.PayrollEmployeeId <= 0)
        {
            ModelState.AddModelError(nameof(model.PayrollEmployeeId), "يجب اختيار موظف.");
        }

        if (model.Amount <= 0)
        {
            ModelState.AddModelError(nameof(model.Amount), "المبلغ يجب أن يكون أكبر من صفر.");
        }

        if (string.IsNullOrWhiteSpace(model.Reason))
        {
            ModelState.AddModelError(nameof(model.Reason), "السبب مطلوب.");
        }

        if (!ModelState.IsValid)
        {
            await PopulatePayrollEmployeesAsync(scope.CompanyNetworkId);
            return View(model);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        model.CreatedByUserId = user?.Id;
        model.Status = EmployeeRewardPenaltyStatus.Pending;
        model.CreatedAt = DateTime.UtcNow;
        _context.EmployeeRewardPenalties.Add(model);
        await _context.SaveChangesAsync();
        await _erpNotifications.NotifyRewardPenaltyPendingAsync(model, user?.Id);
        TempData["Success"] = "تم تسجيل الطلب — بانتظار الاعتماد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Review(int id, bool approve, string? reviewNotes)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        (bool success, string message) = await _rewardPenaltyService.ReviewAsync(
            id, scope.CompanyNetworkId, user?.Id ?? string.Empty, approve, reviewNotes);

        if (success)
        {
            EmployeeRewardPenalty? reviewed = await _context.EmployeeRewardPenalties.AsNoTracking()
                .Include(r => r.PayrollEmployee)
                .FirstOrDefaultAsync(r => r.Id == id && r.CompanyNetworkId == scope.CompanyNetworkId);
            if (reviewed != null)
            {
                await _erpNotifications.NotifyRewardPenaltyReviewedAsync(reviewed, approve);
            }
        }

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    private async Task<CompanyBusinessScopeHelper.CompanyScope?> ResolveScopeAsync()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
        }

        return scope;
    }

    private async Task PopulatePayrollEmployeesAsync(int companyNetworkId)
    {
        ViewBag.PayrollEmployees = await _context.PayrollEmployees.AsNoTracking()
            .Where(e => e.CompanyNetworkId == companyNetworkId && e.IsActive)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem { Value = e.Id.ToString(), Text = e.FullName })
            .ToListAsync();
    }
}
