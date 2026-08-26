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

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NotificationsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool unreadOnly = true)
    {
        ViewData["Title"] = "التنبيهات";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        IQueryable<UserNotification> q = _context.UserNotifications
            .AsNoTracking()
            .Where(n => n.UserId == user.Id);

        if (unreadOnly)
        {
            q = q.Where(n => !n.IsRead);
        }

        List<UserNotification> notifications = await q
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync();

        Dictionary<int, CompanyEmployeeTaskStatus> taskStatuses = await LoadRelatedTaskStatusesAsync(notifications);

        Dictionary<int, bool> canMarkReadById = notifications.ToDictionary(
            n => n.Id,
            n =>
            {
                int? taskId = EmployeeNotificationReadRules.TryParseErpTaskId(n.Key);
                CompanyEmployeeTaskStatus? status = taskId is int id && taskStatuses.TryGetValue(id, out CompanyEmployeeTaskStatus s)
                    ? s
                    : null;
                return EmployeeNotificationReadRules.CanMarkAsRead(n, status);
            });

        ViewBag.UnreadOnly = unreadOnly;
        ViewBag.CanMarkReadById = canMarkReadById;
        return View(notifications);
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { count = 0 });
        }

        int count = await _context.UserNotifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == user.Id && !n.IsRead);

        return Json(new { count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        UserNotification? row = await _context.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
        if (row == null)
        {
            return NotFound();
        }

        if (!await CanMarkAsReadAsync(row))
        {
            TempData["Error"] = "لا يمكن تعليم تنبيه المهمة كمقروء قبل إنجاز المهمة.";
            return RedirectToNotifications(returnUrl);
        }

        row.IsRead = true;
        row.ReadAt = DateTime.Now;
        await _context.SaveChangesAsync();

        return RedirectToNotifications(returnUrl);
    }

    [HttpGet]
    public async Task<IActionResult> Open(int id)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        UserNotification? row = await _context.UserNotifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
        if (row == null)
        {
            return NotFound();
        }

        // لا تُعلَّم المهمة كمقروءة تلقائياً إلا بعد إنجازها.
        if (!row.IsRead && await CanMarkAsReadAsync(row))
        {
            row.IsRead = true;
            row.ReadAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        string? targetUrl = ResolveNotificationTargetUrl(row);
        if (!string.IsNullOrWhiteSpace(targetUrl) && Url.IsLocalUrl(targetUrl))
        {
            return Redirect(targetUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private IActionResult RedirectToNotifications(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToRoute("employee-notifications");
    }

    private async Task<bool> CanMarkAsReadAsync(UserNotification notification)
    {
        int? taskId = EmployeeNotificationReadRules.TryParseErpTaskId(notification.Key);
        CompanyEmployeeTaskStatus? status = null;
        if (taskId is int id)
        {
            status = await _context.CompanyEmployeeTasks.AsNoTracking()
                .Where(t => t.Id == id)
                .Select(t => (CompanyEmployeeTaskStatus?)t.Status)
                .FirstOrDefaultAsync();
        }

        return EmployeeNotificationReadRules.CanMarkAsRead(notification, status);
    }

    private async Task<Dictionary<int, CompanyEmployeeTaskStatus>> LoadRelatedTaskStatusesAsync(
        IReadOnlyList<UserNotification> notifications)
    {
        List<int> taskIds = notifications
            .Select(n => EmployeeNotificationReadRules.TryParseErpTaskId(n.Key))
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (taskIds.Count == 0)
        {
            return new Dictionary<int, CompanyEmployeeTaskStatus>();
        }

        return await _context.CompanyEmployeeTasks.AsNoTracking()
            .Where(t => taskIds.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Status);
    }

    private string? ResolveNotificationTargetUrl(UserNotification notification)
    {
        return notification.Type switch
        {
            NotificationType.ErpTaskAssigned => Url.RouteUrl("employee-my-tasks"),
            NotificationType.ErpRewardPenaltyReviewed => Url.RouteUrl("employee-my-payroll"),
            _ => Url.RouteUrl("employee-notifications"),
        };
    }
}
