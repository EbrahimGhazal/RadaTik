using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Clients;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        /// <summary>
        /// نقل الحسابات المحددة (أو كل المشتركين) إلى برج جديد:
        /// إضافتها على السيرفر المطلوب، تحديث قاعدة البيانات، ثم حذفها من البرج القديم.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> BulkCopyAccountsToServerJson(
            int targetServerId,
            bool applyToAll = false,
            int[]? clientIds = null)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً" });
            }

            if (targetServerId <= 0)
            {
                return Json(new { success = false, status = "InvalidServer", message = "يرجى اختيار السيرفر (البرج) المطلوب." });
            }

            try
            {
                BulkCopyAccountsToServerResult result = await _app.Lifecycle.BulkCopyAccountsToServerAsync(
                    networkId.Value,
                    targetServerId,
                    clientIds,
                    applyToAll);

                return Json(new
                {
                    success = result.Success,
                    status = result.Success
                        ? (result.FailedCount > 0 ? "PartialSuccess" : "Success")
                        : "Failed",
                    message = result.Message,
                    requestedCount = result.RequestedCount,
                    addedCount = result.AddedCount,
                    skippedExistingCount = result.SkippedExistingCount,
                    skippedInvalidCount = result.SkippedInvalidCount,
                    failedCount = result.FailedCount,
                    reassignedCount = result.ReassignedCount,
                    removedFromOldCount = result.RemovedFromOldCount,
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في نقل الحسابات إلى السيرفر {ServerId}", targetServerId);
                return Json(new
                {
                    success = false,
                    status = "Error",
                    message = BuildFriendlyMikroTikErrorMessage("خطأ في نقل الحسابات إلى البرج", ex)
                });
            }
        }
    }
}
