using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class NotificationsController : Controller
    {
        private sealed record PendingLatestCard(string key, string title, int count, string icon, string? url);

        private readonly ApplicationDbContext _context;

        public NotificationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Pending requests counters for SystemAdmin UI badges.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> PendingCounts()
        {
            int joinRequests = await _context.JoinRequests
                .AsNoTracking()
                .CountAsync(j =>
                    (j.RequestType == JoinRequestType.NetworkAdministrator ||
                     j.RequestType == JoinRequestType.CollectionPoint) &&
                    j.Status == JoinRequestStatus.Pending);

            int serviceRequests = await _context.NetworkServiceRequests
                .AsNoTracking()
                .CountAsync(r => r.Status == NetworkServiceRequestStatus.Pending);

            int topUpRequests = await _context.NetworkTopUpRequests
                .AsNoTracking()
                .CountAsync(r => r.Status == NetworkTopUpRequestStatus.Pending);

            int collectionPointTopUps = await _context.CollectionPointTopUpRequests
                .AsNoTracking()
                .CountAsync(r => r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin && r.Status == CollectionPointTopUpStatus.Pending);

            int passwordResetRequests = await _context.PasswordResetRequests
                .AsNoTracking()
                .CountAsync(r => r.ResetMethod == PasswordResetMethod.AdminRequest && r.Status == PasswordResetStatus.Pending);

            int total = joinRequests + serviceRequests + topUpRequests + collectionPointTopUps + passwordResetRequests;
            int operationsTotal = joinRequests + serviceRequests + passwordResetRequests;
            int financeTotal = topUpRequests + collectionPointTopUps;
            int servicesTotal = serviceRequests;
            int companiesTotal = 0;

            PendingLatestCard[] latest = new[]
            {
                new PendingLatestCard("joinRequests", "طلبات الانضمام", joinRequests, "fa-user-plus", Url.RouteUrl("systemAdmin-joinRequests")),
                new PendingLatestCard("serviceRequests", "طلبات الخدمات", serviceRequests, "fa-layer-group", Url.RouteUrl("systemAdmin-serviceRequests")),
                new PendingLatestCard("topUpRequests", "طلبات شحن الشركات", topUpRequests, "fa-wallet", Url.RouteUrl("systemAdmin-topUpRequests")),
                new PendingLatestCard("collectionPointTopUps", "طلبات نقاط التحصيل", collectionPointTopUps, "fa-cash-register", Url.RouteUrl("systemAdmin-collectionPointTopUpRequests")),
                new PendingLatestCard("passwordResetRequests", "طلبات إعادة تعيين كلمة المرور", passwordResetRequests, "fa-key", Url.RouteUrl("systemAdmin-passwordResetRequests"))
            }
            .Where(x => x.count > 0)
            .OrderByDescending(x => x.count)
            .Take(5)
            .ToArray();

            return Json(new
            {
                total,
                joinRequests,
                serviceRequests,
                topUpRequests,
                collectionPointTopUps,
                passwordResetRequests,
                operationsTotal,
                financeTotal,
                servicesTotal,
                companiesTotal,
                latest
            });
        }
    }
}

