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

namespace RadaTik.Areas.CompanyEmployee.Controllers;

/// <summary>مهام ERP المعيّنة للموظف الحالي.</summary>
[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Erp)]
public class MyEmployeeTasksController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MyEmployeeTasksController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CompanyEmployeeTaskStatus? status)
    {
        ViewData["Title"] = "مهامي";
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Dashboard");
        }

        IQueryable<CompanyEmployeeTask> query = _context.CompanyEmployeeTasks.AsNoTracking()
            .Include(t => t.AssignedByUser)
            .Include(t => t.Client)
            .Where(t =>
                t.CompanyNetworkId == scope.CompanyNetworkId
                && t.AssignedToUserId == user.Id);

        if (status.HasValue)
        {
            query = query.Where(t => t.Status == status.Value);
        }
        else
        {
            query = query.Where(t =>
                t.Status == CompanyEmployeeTaskStatus.Pending
                || t.Status == CompanyEmployeeTaskStatus.InProgress);
        }

        ViewBag.StatusFilter = status;
        ViewBag.OpenCount = await _context.CompanyEmployeeTasks.AsNoTracking()
            .CountAsync(t =>
                t.CompanyNetworkId == scope.CompanyNetworkId
                && t.AssignedToUserId == user.Id
                && (t.Status == CompanyEmployeeTaskStatus.Pending
                    || t.Status == CompanyEmployeeTaskStatus.InProgress));

        return View(await query.OrderByDescending(t => t.Priority).ThenBy(t => t.DueDate).ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, CompanyEmployeeTaskStatus status, string? completionNotes)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction(nameof(Index));
        }

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        CompanyEmployeeTask? task = await _context.CompanyEmployeeTasks
            .FirstOrDefaultAsync(t =>
                t.Id == id
                && t.CompanyNetworkId == scope.CompanyNetworkId
                && t.AssignedToUserId == user.Id);
        if (task == null)
        {
            TempData["Error"] = "المهمة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        if (status == CompanyEmployeeTaskStatus.InProgress && task.Status == CompanyEmployeeTaskStatus.Pending)
        {
            task.Status = CompanyEmployeeTaskStatus.InProgress;
        }
        else if (status == CompanyEmployeeTaskStatus.Completed)
        {
            task.Status = CompanyEmployeeTaskStatus.Completed;
            task.CompletedAt = DateTime.UtcNow;
            task.CompletionNotes = completionNotes?.Trim();
        }
        else
        {
            TempData["Error"] = "إجراء غير مسموح.";
            return RedirectToAction(nameof(Index));
        }

        task.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم تحديث المهمة.";
        return RedirectToAction(nameof(Index));
    }
}
