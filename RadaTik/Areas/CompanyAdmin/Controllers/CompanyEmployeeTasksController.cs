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
public class CompanyEmployeeTasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IErpNotificationService _erpNotifications;

    public CompanyEmployeeTasksController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IErpNotificationService erpNotifications)
    {
        _context = context;
        _userManager = userManager;
        _erpNotifications = erpNotifications;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CompanyEmployeeTaskStatus? status)
    {
        ViewData["Title"] = "مهام الموظفين";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        IQueryable<CompanyEmployeeTask> query = _context.CompanyEmployeeTasks.AsNoTracking()
            .Include(t => t.AssignedToUser)
            .Include(t => t.AssignedByUser)
            .Where(t => t.CompanyNetworkId == scope.CompanyNetworkId);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }

        ViewBag.StatusFilter = status;
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(await query.OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "إضافة مهمة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        await PopulateEmployeesAsync(scope.CompanyNetworkId);
        return View(new CompanyEmployeeTask
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            Priority = CompanyEmployeeTaskPriority.Normal,
            Status = CompanyEmployeeTaskStatus.Pending,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CompanyEmployeeTask model)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        model.CompanyNetworkId = scope.CompanyNetworkId;
        model.Title = model.Title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            ModelState.AddModelError(nameof(model.Title), "عنوان المهمة مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(model.AssignedToUserId))
        {
            ModelState.AddModelError(nameof(model.AssignedToUserId), "يجب اختيار موظف.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateEmployeesAsync(scope.CompanyNetworkId);
            return View(model);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        model.AssignedByUserId = user?.Id;
        model.CreatedAt = DateTime.UtcNow;
        model.Status = CompanyEmployeeTaskStatus.Pending;
        _context.CompanyEmployeeTasks.Add(model);
        await _context.SaveChangesAsync();
        await _erpNotifications.NotifyTaskAssignedAsync(model);
        TempData["Success"] = "تم إنشاء المهمة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, CompanyEmployeeTaskStatus status, string? completionNotes)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        CompanyEmployeeTask? task = await _context.CompanyEmployeeTasks
            .FirstOrDefaultAsync(t => t.Id == id && t.CompanyNetworkId == scope.CompanyNetworkId);
        if (task == null)
        {
            return NotFound();
        }

        task.Status = status;
        task.UpdatedAt = DateTime.UtcNow;
        if (status == CompanyEmployeeTaskStatus.Completed)
        {
            task.CompletedAt = DateTime.UtcNow;
            task.CompletionNotes = completionNotes?.Trim();
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تحديث حالة المهمة.";
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

    private async Task PopulateEmployeesAsync(int companyNetworkId)
    {
        List<int> networkIds = await _context.Networks.AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync();

        ViewBag.Employees = await _context.Users.AsNoTracking()
            .Where(u => u.NetworkId != null && networkIds.Contains(u.NetworkId.Value) && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.FullName ?? u.UserName ?? u.Id,
            })
            .ToListAsync();
    }
}
