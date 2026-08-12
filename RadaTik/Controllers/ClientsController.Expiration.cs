using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Clients;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        public async Task<IActionResult> ExpiredAccounts()
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                bool canView = await _app.Permission.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            ClientExpiredAccountsPageModel page =
                await _app.Expiration.BuildExpiredAccountsPageAsync(networkId.Value);
            ViewBag.TotalExpired = page.TotalExpired;
            ViewBag.ActiveExpired = page.ActiveExpired;
            ViewBag.DisabledExpired = page.DisabledExpired;
            return View(page.Accounts);
        }

        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        public async Task<IActionResult> ExpiringIn3Days()
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                bool canView = await _app.Permission.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            ClientExpiringSoonPageModel page =
                await _app.Expiration.BuildExpiringIn3DaysPageAsync(networkId.Value);
            ViewBag.TotalExpiring = page.TotalExpiring;
            ViewBag.ExpiringToday = page.ExpiringToday;
            ViewBag.ExpiringTomorrow = page.ExpiringTomorrow;
            ViewBag.ExpiringIn2Days = page.ExpiringIn2Days;
            ViewBag.ExpiringIn3Days = page.ExpiringIn3Days;
            return View(page.Accounts);
        }

        /// <summary>
        /// تعيين تاريخ انتهاء صلاحية لمشترك واحد (JSON).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> SetExpirationDateJson(int clientId, DateTime expirationDate)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً", clientId });
            }

            if (expirationDate == default)
            {
                return Json(new { success = false, status = "InvalidDate", message = "تاريخ انتهاء الصلاحية غير صالح", clientId });
            }

            DateTime dateOnly = expirationDate.Date;

            try
            {
                Client? client = await _context.Clients.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId.Value);
                string clientLabel = client?.Name ?? client?.UserName ?? $"#{clientId}";

                ClientOperationOutcome outcome = await _app.Lifecycle.SetAccountExpirationDateAsync(
                    clientId,
                    networkId.Value,
                    dateOnly);

                return Json(new
                {
                    success = outcome.IsSuccess,
                    status = outcome.IsSuccess ? "Success" : (outcome.NotFound ? "NotFound" : "Failed"),
                    clientId,
                    clientName = clientLabel,
                    expirationDate = dateOnly.ToString("yyyy-MM-dd"),
                    message = outcome.IsSuccess
                        ? (outcome.SuccessMessage ?? $"تم التحديث حتى {dateOnly:yyyy/MM/dd}")
                        : (outcome.ErrorMessage ?? "فشل تحديث تاريخ الانتهاء")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تعيين تاريخ انتهاء الصلاحية للعميل {ClientId}", clientId);
                return Json(new
                {
                    success = false,
                    status = "Error",
                    clientId,
                    message = BuildFriendlyMikroTikErrorMessage("خطأ في تحديث تاريخ الانتهاء", ex)
                });
            }
        }

        /// <summary>
        /// تحديث جماعي سريع لتاريخ الانتهاء (طلب واحد — تحديث SQL جماعي).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> BulkSetExpirationDateJson(
            DateTime expirationDate,
            bool applyToAll = false,
            int[]? clientIds = null)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً" });
            }

            if (expirationDate == default)
            {
                return Json(new { success = false, status = "InvalidDate", message = "تاريخ انتهاء الصلاحية غير صالح" });
            }

            try
            {
                BulkExpirationUpdateResult result = await _app.Lifecycle.BulkSetAccountExpirationAsync(
                    networkId.Value,
                    clientIds,
                    expirationDate.Date,
                    applyToAll);

                return Json(new
                {
                    success = result.Success,
                    status = result.Success ? "Success" : "Failed",
                    updatedCount = result.UpdatedCount,
                    requestedCount = result.RequestedCount,
                    expirationDate = expirationDate.Date.ToString("yyyy-MM-dd"),
                    message = result.Message
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في التحديث الجماعي لتاريخ انتهاء الصلاحية");
                return Json(new
                {
                    success = false,
                    status = "Error",
                    message = BuildFriendlyMikroTikErrorMessage("خطأ في التحديث الجماعي", ex)
                });
            }
        }
    }
}
