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
        /// إنهاء النسخ الاحتياطي: إعادة السيرفر الفعّال إلى البرج الأساسي مع حذف الحسابات الاحتياطية اختيارياً.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> EndFailover(int id, bool removeStandbyAccounts = true)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            ClientOperationOutcome outcome = await _app.Lifecycle.EndFailoverAsync(
                id,
                networkId.Value,
                removeStandbyAccounts);

            if (outcome.NotFound)
            {
                return NotFound();
            }

            if (!outcome.IsSuccess)
            {
                TempData["Error"] = outcome.ErrorMessage;
            }
            else
            {
                TempData["Success"] = outcome.SuccessMessage;
            }

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
