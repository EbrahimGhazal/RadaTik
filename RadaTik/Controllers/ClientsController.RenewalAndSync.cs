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
        // POST: Clients/ToggleStatus/5
        [HttpPost]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            ClientOperationOutcome outcome = await _app.Lifecycle.ToggleActiveAsync(id, networkId.Value);
            return ApplyClientOperationOutcome(outcome, nameof(Index));
        }

        // POST: Clients/Freeze/5 - تجميد الحساب
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee,EmployeeLegacy")]
        public async Task<IActionResult> Freeze(int id)
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canEdit = await _app.Permission.HasPermissionAsync(User, "Clients.Edit");
                if (!canEdit)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            try
            {
                ClientOperationOutcome outcome = await _app.Lifecycle.FreezeAsync(id, networkId.Value);
                return ApplyClientOperationOutcome(outcome, nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تجميد الحساب للعميل {ClientId}", id);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجميد الحساب", ex)}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // POST: Clients/Unfreeze/5 - تفعيل الحساب
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee,EmployeeLegacy")]
        public async Task<IActionResult> Unfreeze(int id)
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canEdit = await _app.Permission.HasPermissionAsync(User, "Clients.Edit");
                if (!canEdit)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            try
            {
                ClientOperationOutcome outcome = await _app.Lifecycle.UnfreezeAsync(id, networkId.Value);
                return ApplyClientOperationOutcome(outcome, nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تفعيل الحساب للعميل {ClientId}", id);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تفعيل الحساب", ex)}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // POST: Clients/RenewOneMonth/5 - تجديد شهر من تاريخ الانتهاء الحالي
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee,EmployeeLegacy")]
        public async Task<IActionResult> RenewOneMonth(int id)
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canEdit = await _app.Permission.HasPermissionAsync(User, "Clients.Edit");
                if (!canEdit)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            try
            {
                ClientOperationOutcome outcome = await _app.Lifecycle.RenewOneMonthAsync(id, networkId.Value);
                return ApplyClientOperationOutcome(outcome, nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في التجديد الشهري للعميل {ClientId}", id);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجديد الاشتراك", ex)}";
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Clients/RenewSubscription/5
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RenewSubscription(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            ClientRenewSubscriptionPageModel? page = await _app.Lifecycle.BuildRenewSubscriptionPageAsync(
                id.Value,
                networkId.Value);
            if (page == null)
            {
                return NotFound();
            }

            ViewBag.ClientId = page.ClientId;
            ViewBag.ClientName = page.ClientName;
            ViewBag.CurrentExpirationDate = page.CurrentExpirationDate;

            return View();
        }

        // POST: Clients/RenewSubscription/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RenewSubscription(int id, DateTime? expirationDate, int? renewDays)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            try
            {
                ClientOperationOutcome outcome = await _app.Lifecycle.RenewSubscriptionAsync(
                    id,
                    networkId.Value,
                    expirationDate,
                    renewDays);
                return ApplyClientOperationOutcome(outcome, nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تجديد الاشتراك للعميل {ClientId}", id);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجديد الاشتراك", ex)}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // POST: Clients/RenewTo8thNextMonth/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RenewTo8thNextMonth(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            try
            {
                ClientOperationOutcome outcome = await _app.Lifecycle.RenewTo8thNextMonthAsync(id, networkId.Value);
                return ApplyClientOperationOutcome(outcome, nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تجديد الاشتراك للعميل {ClientId}", id);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجديد الاشتراك", ex)}";
                return RedirectToAction(nameof(Details), new { id });
            }
        }

        // GET: Clients/SyncWithMikroTik/5
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> SyncWithMikroTik(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            ClientOperationOutcome outcome = await _app.Lifecycle.SyncWithMikroTikAsync(id.Value, networkId.Value);
            return ApplyClientOperationOutcome(outcome, nameof(Details), new { id });
        }

    }
}
