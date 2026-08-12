using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Models;
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

        ViewBag.UnreadOnly = unreadOnly;
        return View(await q.OrderByDescending(n => n.CreatedAt).Take(200).ToListAsync());
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

        row.IsRead = true;
        row.ReadAt = DateTime.Now;
        await _context.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction(nameof(Index));
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

        if (!row.IsRead)
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
