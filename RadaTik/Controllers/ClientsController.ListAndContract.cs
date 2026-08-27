using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.PricingPreview;
using RadaTik.Helpers;
using RadaTik.Security;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        // GET: Clients
        public async Task<IActionResult> Index()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            IReadOnlyList<string> userRoles = (await _userManager.GetRolesAsync(user)).ToList();
            int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            ClientIndexPageModel page = await _app.ListQuery.BuildIndexPageAsync(
                user,
                User,
                userRoles,
                selectedNetworkId);

            return page.Access switch
            {
                ClientListAccessOutcome.Forbidden => Forbid(),
                ClientListAccessOutcome.RequiresNetworkSelection => RedirectToNetworkIndexWithError(),
                _ => BindIndexView(page)
            };
        }

        /// <summary>
        /// حالة الاتصال الحية (لا تُستدعى أثناء Render الصفحة حتى لا تبطئها).
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ConnectionStatusJson(bool forceRefresh = false)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized();
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return BadRequest(new { success = false, message = "يرجى تحديد شبكة أولاً" });
            }

            try
            {
                HashSet<int> connectedIds = await _app.ListQuery.GetLiveConnectedClientIdsAsync(
                    networkId.Value,
                    forceRefresh);
                return Json(new
                {
                    success = true,
                    connectedIds = connectedIds.ToArray(),
                    connectedCount = connectedIds.Count
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل جلب حالة الاتصال للشبكة {NetworkId}", networkId);
                return StatusCode(500, new { success = false, message = "تعذر جلب حالة الاتصال" });
            }
        }

        private IActionResult RedirectToNetworkIndexWithError()
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network");
        }

        private IActionResult BindIndexView(ClientIndexPageModel page)
        {
            ViewBag.DbAccountMap = page.DbAccountMap;
            ViewBag.PendingClientIds = page.PendingClientIds;
            ViewBag.ConnectedClientIds = page.ConnectedClientIds;
            ViewBag.ConnectionsReady = page.ConnectionsReady;
            ViewBag.Networks = page.AvailableNetworks;
            ViewBag.CurrentNetworkId = page.CurrentNetworkId;
            ViewBag.CopyTargetServers = page.CopyTargetServers;
            return View(page.Clients);
        }

        // GET: Clients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            IReadOnlyList<string> userRoles = (await _userManager.GetRolesAsync(user)).ToList();
            bool canLoadMikroTik = User.IsInRole(RoleNames.NetworkAdministrator);

            ClientDetailsPageModel page;
            try
            {
                page = await _app.ListQuery.BuildDetailsPageAsync(
                    id.Value,
                    user,
                    User,
                    userRoles,
                    canLoadMikroTik);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "فشل تحميل صفحة تفاصيل العميل {ClientId}", id);
                page = await BuildDetailsFallbackAsync(id.Value, userRoles);
            }

            return page.Access switch
            {
                ClientListAccessOutcome.Forbidden => Forbid(),
                ClientListAccessOutcome.NotFound => NotFound(),
                _ => BindDetailsView(page)
            };
        }

        private async Task<ClientDetailsPageModel> BuildDetailsFallbackAsync(
            int clientId,
            IReadOnlyList<string> userRoles)
        {
            Client? client = await _context.Clients
                .Include(c => c.Receiver)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Profile)
                .FirstOrDefaultAsync(m => m.Id == clientId);

            if (client == null)
            {
                return new ClientDetailsPageModel { Access = ClientListAccessOutcome.NotFound };
            }

            bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) ||
                              userRoles.Contains(RoleNames.EmployeeLegacy);
            bool isClientOnly = userRoles.Contains(RoleNames.Client) && !isEmployee &&
                                !userRoles.Contains(RoleNames.NetworkAdministrator);

            return new ClientDetailsPageModel
            {
                Access = ClientListAccessOutcome.Ok,
                Client = client,
                IsClientOnly = isClientOnly,
                CanEditClient = !isClientOnly,
                MikroTikError = "تعذر تحميل بعض البيانات الإضافية. يتم عرض بيانات المشترك المحفوظة."
            };
        }

        private IActionResult BindDetailsView(ClientDetailsPageModel page)
        {
            ViewBag.IsPendingClientApproval = page.IsPendingClientApproval;
            ViewBag.RenewalBlockedMessage = page.RenewalBlockedMessage;
            ViewBag.MikroTikInfo = page.MikroTikInfo;
            ViewBag.MikroTikError = page.MikroTikError;
            ViewBag.IsClientView = page.IsClientView;
            ViewBag.IsClientOnly = page.IsClientOnly;
            ViewBag.CanEditClient = page.CanEditClient;
            ViewBag.RecentTopUps = page.RecentTopUps;
            return View(page.Client);
        }

        /// <summary>
        /// نقطة توافق قديمة: موعد التركيب أصبح مرتبطاً تلقائياً بتاريخ إضافة العميل (CreatedDate).
        /// هذه العملية لا تحفظ أي قيمة جديدة وتعيد رسالة توضيحية فقط.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> SetScheduledInstallationDate(int id, DateTime? scheduledInstallationDate)
        {
            _ = scheduledInstallationDate;
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client == null)
            {
                return NotFound();
            }

            TempData["Info"] = $"موعد التركيب للعميل «{client.Name ?? client.UserName ?? client.Id.ToString()}» مرتبط تلقائياً بتاريخ الإضافة: {client.CreatedDate:yyyy/MM/dd HH:mm}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>تغذية رصيد العميل - من مدير النظام أو مدير الشبكة</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
        public async Task<IActionResult> TopUpBalance(int id, decimal amount, string? notes)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            bool isSystemAdmin = User.IsInRole(RoleNames.SystemAdministrator);
            bool isNetworkManager = User.IsInRole(RoleNames.NetworkAdministrator);
            if (!isSystemAdmin && !isNetworkManager)
            {
                TempData["Error"] = "غير مصرح بتغذية الرصيد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ClientTopUpSource sourceType = isSystemAdmin
                ? ClientTopUpSource.SystemAdmin
                : ClientTopUpSource.NetworkManager;
            int? actorNetworkId = isNetworkManager
                ? NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user)
                : null;

            try
            {
                ClientWalletTopUpOutcome outcome = await _app.WalletTopUp.TopUpAsync(new ClientWalletTopUpCommand
                {
                    ClientId = id,
                    Amount = amount,
                    ActorUserId = user.Id,
                    SourceType = sourceType,
                    ActorNetworkId = actorNetworkId,
                    Notes = notes,
                    ActorDisplayName = user.FullName ?? user.UserName
                });
                return ApplyWalletTopUpOutcome(outcome, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تغذية رصيد العميل {ClientId}", id);
                TempData["Error"] = "حدث خطأ أثناء تغذية الرصيد.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
        public async Task<IActionResult> GiftVipBalance(int id, decimal amount, string? notes)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            Client? client = await _context.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
            if (client == null)
            {
                return NotFound();
            }

            if (!client.IsVip)
            {
                TempData["Error"] = "الهدية المالية متاحة للمشتركين المميزين فقط. فعّل VIP أولاً.";
                return RedirectToAction(nameof(Details), new { id });
            }

            bool isSystemAdmin = User.IsInRole(RoleNames.SystemAdministrator);
            ClientTopUpSource sourceType = isSystemAdmin
                ? ClientTopUpSource.SystemAdmin
                : ClientTopUpSource.NetworkManager;
            int? actorNetworkId = isSystemAdmin
                ? null
                : NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            string giftNotes = string.IsNullOrWhiteSpace(notes)
                ? "هدية VIP"
                : $"هدية VIP: {notes.Trim()}";

            try
            {
                ClientWalletTopUpOutcome outcome = await _app.WalletTopUp.TopUpAsync(new ClientWalletTopUpCommand
                {
                    ClientId = id,
                    Amount = amount,
                    ActorUserId = user.Id,
                    SourceType = sourceType,
                    ActorNetworkId = actorNetworkId,
                    Notes = giftNotes,
                    ActorDisplayName = user.FullName ?? user.UserName
                });
                return ApplyWalletTopUpOutcome(outcome, id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في هدية VIP للعميل {ClientId}", id);
                TempData["Error"] = "حدث خطأ أثناء منح هدية VIP.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        /// <summary>تجديد الاشتراك ذاتياً من قبل المشترك: حسم سعر الباقة من رصيد محفظته وتمديد شهر</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNames.Client)]
        public async Task<IActionResult> SelfRenewSubscription(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || user.ClientId != id)
            {
                TempData["Error"] = "غير مصرح بتجديد هذا الحساب.";
                return RedirectToAction("Index", "Home");
            }

            try
            {
                ClientOperationOutcome outcome = await _app.SelfRenewal.RenewFromWalletAsync(id);
                return ApplyClientOperationOutcome(outcome, nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في التجديد الذاتي للعميل {ClientId}", id);
                TempData["Error"] = "حدث خطأ أثناء التجديد. يرجى المحاولة لاحقاً أو التواصل مع الإدارة.";
                return RedirectToAction(nameof(Details), new { id });
            }
        }
    }
}
