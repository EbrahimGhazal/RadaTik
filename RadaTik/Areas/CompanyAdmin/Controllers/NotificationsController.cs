using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
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

        List<UserNotification> list = await q
            .OrderByDescending(n => n.CreatedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.UnreadOnly = unreadOnly;
        return View(list);
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

        UserNotification? row = await _context.UserNotifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
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

        string? targetUrl = await ResolveNotificationTargetUrlAsync(row);
        if (!string.IsNullOrWhiteSpace(targetUrl) && Url.IsLocalUrl(targetUrl))
        {
            return Redirect(targetUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<string?> ResolveNotificationTargetUrlAsync(UserNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Key) &&
            notification.Key.StartsWith("EmployeeSectorCreatePending:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = (notification.Key ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2 && int.TryParse(parts[1], out int sectorId))
            {
                int? requestId = await _context.NetworkServiceRequests
                    .AsNoTracking()
                    .Where(r =>
                        r.Status == NetworkServiceRequestStatus.Pending &&
                        r.FeatureKey == FeatureKeys.Sectors &&
                        r.Notes != null &&
                        r.Notes.Contains($"SECTOR_CREATE_PENDING:{sectorId}"))
                    .OrderByDescending(r => r.RequestedAt)
                    .Select(r => (int?)r.Id)
                    .FirstOrDefaultAsync();

                if (requestId.HasValue)
                {
                    return Url.Action("Index", "EmployeeServiceApprovals", new { area = "CompanyAdmin", focusRequestId = requestId.Value });
                }
            }

            return Url.Action("Index", "EmployeeServiceApprovals", new { area = "CompanyAdmin" });
        }

        if (!string.IsNullOrWhiteSpace(notification.Key) &&
            notification.Key.StartsWith("EmployeeApprovalPending:", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = (notification.Key ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && int.TryParse(parts[2], out int requestId))
            {
                return Url.Action("Index", "EmployeeServiceApprovals", new { area = "CompanyAdmin", focusRequestId = requestId });
            }

            return Url.Action("Index", "EmployeeServiceApprovals", new { area = "CompanyAdmin" });
        }

        switch (notification.Type)
        {
            case NotificationType.SubscriptionExpiring:
                {
                    // جميع الخدمات مجانية في التجربة، لذا نوجّه إلى صفحة الخدمات مباشرةً.
                    return Url.RouteUrl("networkManager-features");
                }
            case NotificationType.MaintenanceRequestSubmitted:
                return Url.RouteUrl("networkManager-requestsManagement", new { action = "MaintenanceRequests" });
            case NotificationType.SpeedChangeRequestSubmitted:
                {
                    // محاولة إيجاد رقم العميل من خلال رقم الطلب المخزن في مفتاح الإشعار
                    // تنسيق المفتاح من خدمة الإشعارات: "SpeedChangeRequestSubmitted:{request.Id}"
                    try
                    {
                        string[] parts = (notification.Key ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && int.TryParse(parts[1], out int parsedRequestId))
                        {
                            // الانتقال المفضل: تفاصيل طلب تغيير السرعة مباشرة
                            return Url.RouteUrl("networkManager-requestsManagement", new
                            {
                                action = "SpeedChangeRequestDetails",
                                id = parsedRequestId
                            });
                        }
                    }
                    catch
                    {
                        // نتجاهل أي خطأ في التحليل ونستخدم المسار الاحتياطي
                    }

                    // في حال لم نتمكن من تحديد العميل، نعود إلى صفحة إدارة طلبات تغيير السرعة
                    return Url.RouteUrl("networkManager-requestsManagement", new { action = "SpeedChangeRequests" });
                }
            case NotificationType.ClientTopUpSubmitted:
                {
                    int? clientId = TryParseEntityId(notification.Key);
                    if (clientId.HasValue)
                    {
                        return Url.RouteUrl("networkManager-clients", new { action = "Details", id = clientId.Value });
                    }

                    return Url.RouteUrl("networkManager-clients", new { action = "Index" });
                }
            case NotificationType.ClientJoinRequestSubmitted:
            case NotificationType.EmployeeJoinRequestSubmitted:
                return Url.Action("Index", "JoinRequests", new { area = "", type = "Client" });
            case NotificationType.CollectionPointTopUpRequestSubmitted:
                return Url.RouteUrl("networkManager-collectionpoints", new { action = "TopUpRequests" });
            case NotificationType.ClientWalletTopUpRequestSubmitted:
                {
                    int? requestId = TryParseEntityId(notification.Key);
                    if (requestId.HasValue)
                    {
                        int? clientId = await _context.ClientWalletTopUpRequests
                            .AsNoTracking()
                            .Where(r => r.Id == requestId.Value)
                            .Select(r => (int?)r.ClientId)
                            .FirstOrDefaultAsync();

                        if (clientId.HasValue)
                        {
                            return Url.RouteUrl("networkManager-clients", new { action = "Details", id = clientId.Value });
                        }
                    }

                    return Url.RouteUrl("networkManager-clients", new { action = "Index" });
                }
            case NotificationType.MaintenanceInvoiceIssued:
                {
                    int? invoiceId = TryParseEntityId(notification.Key);
                    if (invoiceId.HasValue)
                    {
                        return Url.RouteUrl("clientPortal-actions", new { action = "MaintenanceInvoices", id = invoiceId.Value });
                    }

                    return Url.RouteUrl("clientPortal-actions", new { action = "MaintenanceInvoices" });
                }
            case NotificationType.MaintenanceInvoicePaid:
                return Url.RouteUrl("networkManager-requestsManagement", new { action = "MaintenanceRequests" });
            case NotificationType.ErpTaskAssigned:
                return Url.RouteUrl("employee-my-tasks");
            case NotificationType.ErpRewardPenaltyPending:
                return Url.RouteUrl("networkManager-erp-rewards");
            case NotificationType.ErpRewardPenaltyReviewed:
                return Url.RouteUrl("employee-my-payroll");
            default:
                return Url.RouteUrl("networkManager-notifications");
        }
    }

    private static int? TryParseEntityId(string? key)
    {
        string[] parts = (key ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
        {
            return null;
        }

        return int.TryParse(parts[1], out int id) ? id : null;
    }
}

