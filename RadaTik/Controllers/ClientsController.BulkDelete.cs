using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Clients;

namespace RadaTik.Controllers;

public partial class ClientsController
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
    [RequirePermission("Clients.Delete")]
    public async Task<IActionResult> BulkDeleteSelectedJson(int[]? clientIds = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            return Json(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً" });
        }

        try
        {
            BulkDeleteClientsResult result = await _app.Provisioning.BulkDeleteSelectedAsync(
                networkId.Value,
                clientIds);

            return Json(new
            {
                success = result.Success,
                status = result.Success
                    ? (result.FailedCount > 0 || result.MikroTikWarningCount > 0 ? "PartialSuccess" : "Success")
                    : "Failed",
                message = result.Message,
                requestedCount = result.RequestedCount,
                deletedCount = result.DeletedCount,
                failedCount = result.FailedCount,
                notFoundCount = result.NotFoundCount,
                mikroTikWarningCount = result.MikroTikWarningCount,
                errors = result.Errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في حذف المشتركين المحددين");
            return Json(new
            {
                success = false,
                status = "Error",
                message = "تعذر حذف المشتركين المحددين. حاول مرة أخرى."
            });
        }
    }
}
