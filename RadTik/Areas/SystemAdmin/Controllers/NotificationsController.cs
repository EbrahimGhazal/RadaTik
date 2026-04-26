using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class NotificationsController : Controller
    {
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
            var joinRequests = await _context.JoinRequests
                .AsNoTracking()
                .CountAsync(j => j.RequestType == JoinRequestType.NetworkAdministrator && j.Status == JoinRequestStatus.Pending);

            var serviceRequests = await _context.NetworkServiceRequests
                .AsNoTracking()
                .CountAsync(r => r.Status == NetworkServiceRequestStatus.Pending);

            var topUpRequests = await _context.NetworkTopUpRequests
                .AsNoTracking()
                .CountAsync(r => r.Status == NetworkTopUpRequestStatus.Pending);

            var collectionPointTopUps = await _context.CollectionPointTopUpRequests
                .AsNoTracking()
                .CountAsync(r => r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin && r.Status == CollectionPointTopUpStatus.Pending);

            var passwordResetRequests = await _context.PasswordResetRequests
                .AsNoTracking()
                .CountAsync(r => r.ResetMethod == PasswordResetMethod.AdminRequest && r.Status == PasswordResetStatus.Pending);

            var total = joinRequests + serviceRequests + topUpRequests + collectionPointTopUps + passwordResetRequests;
            var operationsTotal = serviceRequests + passwordResetRequests;
            var financeTotal = topUpRequests + collectionPointTopUps;
            var servicesTotal = serviceRequests;
            var companiesTotal = joinRequests;

            var latest = new[]
            {
                new
                {
                    key = "joinRequests",
                    title = "طلبات مديري الشركات",
                    count = joinRequests,
                    icon = "fa-user-shield",
                    url = Url.RouteUrl("systemAdmin-joinRequests", new { type = "NetworkAdministrator" })
                },
                new
                {
                    key = "serviceRequests",
                    title = "طلبات الخدمات",
                    count = serviceRequests,
                    icon = "fa-layer-group",
                    url = Url.RouteUrl("systemAdmin-serviceRequests")
                },
                new
                {
                    key = "topUpRequests",
                    title = "طلبات شحن الشركات",
                    count = topUpRequests,
                    icon = "fa-wallet",
                    url = Url.RouteUrl("systemAdmin-topUpRequests")
                },
                new
                {
                    key = "collectionPointTopUps",
                    title = "طلبات نقاط التحصيل",
                    count = collectionPointTopUps,
                    icon = "fa-cash-register",
                    url = Url.RouteUrl("systemAdmin-collectionPointTopUpRequests")
                },
                new
                {
                    key = "passwordResetRequests",
                    title = "طلبات إعادة تعيين كلمة المرور",
                    count = passwordResetRequests,
                    icon = "fa-key",
                    url = Url.RouteUrl("systemAdmin-passwordResetRequests")
                }
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

