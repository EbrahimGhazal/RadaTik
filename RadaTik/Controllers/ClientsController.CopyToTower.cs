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
        /// نقل أو نسخ الحسابات المحددة إلى برج جديد.
        /// removeFromSource=true ينقل ويحذف من البرج القديم.
        /// removeFromSource=false ينسخ ويبقي المشتركين على البرج القديم.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> BulkCopyAccountsToServerJson(
            int targetServerId,
            bool applyToAll = false,
            bool removeFromSource = true,
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

            string actionLabel = removeFromSource ? "نقل" : "نسخ";
            try
            {
                BulkCopyAccountsToServerResult result = await _app.Lifecycle.BulkCopyAccountsToServerAsync(
                    networkId.Value,
                    targetServerId,
                    clientIds,
                    applyToAll,
                    removeFromSource);

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
                    clonedCount = result.ClonedCount,
                    errors = result.Errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في {Action} الحسابات إلى السيرفر {ServerId}", actionLabel, targetServerId);
                return Json(new
                {
                    success = false,
                    status = "Error",
                    message = BuildFriendlyMikroTikErrorMessage($"خطأ في {actionLabel} الحسابات إلى البرج", ex)
                });
            }
        }
    }
}
