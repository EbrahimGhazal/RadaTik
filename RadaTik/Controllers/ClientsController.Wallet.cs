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
        /// شحن رصيد جماعي للمشتركين (مبلغ ثابت أو نسبة من سعر الباقة).
        /// يمر عبر نفس مسار TopUpAsync لتطبيق قواعد المحفظة المالية.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
        public async Task<IActionResult> BulkTopUpBalanceJson(
            string mode,
            decimal value,
            bool applyToAll = false,
            int[]? clientIds = null,
            string? notes = null)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { success = false, status = "Unauthorized", message = "يجب تسجيل الدخول." });
            }

            bool isSystemAdmin = User.IsInRole(RoleNames.SystemAdministrator);
            bool isNetworkManager = User.IsInRole(RoleNames.NetworkAdministrator);
            if (!isSystemAdmin && !isNetworkManager)
            {
                return Json(new { success = false, status = "Forbidden", message = "غير مصرح بشحن الرصيد." });
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                return Json(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً" });
            }

            BulkTopUpMode topUpMode;
            if (string.Equals(mode, "percent", StringComparison.OrdinalIgnoreCase)
                || string.Equals(mode, "PercentOfPackage", StringComparison.OrdinalIgnoreCase))
            {
                topUpMode = BulkTopUpMode.PercentOfPackage;
            }
            else if (string.Equals(mode, "fixed", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(mode, "Fixed", StringComparison.OrdinalIgnoreCase))
            {
                topUpMode = BulkTopUpMode.Fixed;
            }
            else
            {
                return Json(new { success = false, status = "InvalidMode", message = "نوع الشحن غير صالح (ثابت أو نسبة)." });
            }

            ClientTopUpSource sourceType = isSystemAdmin
                ? ClientTopUpSource.SystemAdmin
                : ClientTopUpSource.NetworkManager;
            int? actorNetworkId = isNetworkManager ? networkId : null;

            try
            {
                BulkClientWalletTopUpOutcome outcome = await _app.WalletTopUp.BulkTopUpAsync(
                    new BulkClientWalletTopUpCommand
                    {
                        NetworkId = networkId.Value,
                        ApplyToAll = applyToAll,
                        ClientIds = clientIds,
                        Mode = topUpMode,
                        Value = value,
                        ActorUserId = user.Id,
                        SourceType = sourceType,
                        ActorNetworkId = actorNetworkId,
                        Notes = notes,
                        ActorDisplayName = user.FullName ?? user.UserName
                    });

                return Json(new
                {
                    success = outcome.IsSuccess,
                    status = outcome.IsSuccess
                        ? (outcome.FailedCount > 0 ? "PartialSuccess" : "Success")
                        : "Failed",
                    message = outcome.Message,
                    requestedCount = outcome.RequestedCount,
                    succeededCount = outcome.SucceededCount,
                    skippedCount = outcome.SkippedCount,
                    failedCount = outcome.FailedCount,
                    totalCredited = outcome.TotalCredited,
                    errors = outcome.Errors.Take(15).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في الشحن الجماعي لأرصدة المشتركين");
                return Json(new
                {
                    success = false,
                    status = "Error",
                    message = "حدث خطأ أثناء الشحن الجماعي للأرصدة."
                });
            }
        }
    }
}
