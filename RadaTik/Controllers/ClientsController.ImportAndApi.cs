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
        // GET: Clients/CheckExpiredAccounts
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> CheckExpiredAccounts()
        {
            try
            {
                var result = await _app.MikroTikPppoe.CheckAndDisableExpiredAccounts();

                if (result.Success)
                {
                    if (result.DisabledAccounts.Count > 0)
                    {
                        TempData["Success"] = $"✅ {result.Message} - تم إيقاف {result.DisabledAccounts.Count} حساب";
                    }
                    else
                    {
                        TempData["Info"] = $"✅ {result.Message} - لا توجد حسابات منتهية الصلاحية";
                    }
                }
                else
                {
                    TempData["Error"] = $"❌ {result.Message}";
                }

                // إذا كان هناك حسابات متوقفة، يمكن عرضها
                if (result.DisabledAccounts.Count > 0)
                {
                    TempData["ExpiredAccounts"] = System.Text.Json.JsonSerializer.Serialize(result.DisabledAccounts);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في التحقق من الحسابات المنتهية", ex)}";
                _logger.LogError(ex, "خطأ في التحقق من الحسابات المنتهية");
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Clients/ImportFromServer - صفحة اختيار السيرفر واستيراد المشتركين
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.ImportFromServer")]
        public async Task<IActionResult> ImportFromServer()
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canView = await _app.Permission.HasPermissionAsync(User, "Clients.View");
                if (!canView)
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

            List<MikroTikServer> servers = await _context.MikroTikServers
                .AsNoTracking()
                .Where(s => s.NetworkId == networkId.Value)
                .OrderBy(s => s.Name)
                .ToListAsync();

            try
            {
                ClientImportFromServerViewModel view = await _app.Import.BuildImportFromServerViewAsync(networkId.Value);
                servers = view.Servers.ToList();
                ViewBag.ImportPreviewByServer = view.ImportPage.PreviewByServer;
                ViewBag.ImportChargeByServer = view.ImportPage.ChargeByServer;
                ViewBag.ClientImportUnitPrice = view.ImportPage.SubscriberUnitPrice;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "تعذر بناء معاينة الاستيراد لكل السيرفرات. ستُعرض القائمة مع تخطي المعاينة.");
                ViewBag.ImportPreviewByServer = new Dictionary<int, ImportUsersPreviewResult>();
                ViewBag.ImportChargeByServer = new Dictionary<int, UsageImportChargeEstimate>();
                ViewBag.ClientImportUnitPrice = 0m;
                TempData["Info"] = "تعذر الاتصال ببعض السيرفرات أثناء المعاينة. يمكنك المتابعة ومزامنة السيرفرات المتاحة.";
            }

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = networkId;
            return View(servers);
        }

        // POST: Clients/ImportFromServer - تنفيذ الاستيراد من السيرفر المحدد
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.ImportFromServer")]
        public async Task<IActionResult> ImportFromServer(int serverId)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

            if (server == null)
            {
                TempData["Error"] = "السيرفر غير موجود أو لا يتبع الشبكة الحالية";
                return RedirectToAction(nameof(ImportFromServer));
            }

            try
            {
                ClientImportOutcome outcome = await _app.Import.ExecuteImportAsync(
                    serverId,
                    networkId.Value,
                    user!.Id,
                    rejectWhenProfilesMissing: false);
                ApplyClientImportOutcome(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في استيراد المشتركين من السيرفر {ServerId}", serverId);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في الاستيراد", ex)}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Clients/ImportFromServerJson — استيراد خادم واحد مع نتيجة JSON لتقدّم الواجهة
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.ImportFromServer")]
        public async Task<IActionResult> ImportFromServerJson(int serverId)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                return Json(new { success = false, status = "NetworkRequired", message = "يرجى تحديد شبكة أولاً", serverId });
            }

            var server = await _context.MikroTikServers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);
            string serverLabel = server?.Name ?? $"#{serverId}";

            if (server == null)
            {
                return Json(new
                {
                    success = false,
                    status = "ServerNotFound",
                    serverId,
                    serverName = serverLabel,
                    message = "السيرفر غير موجود أو لا يتبع الشبكة الحالية"
                });
            }

            try
            {
                ClientImportOutcome outcome = await _app.Import.ExecuteImportAsync(
                    serverId,
                    networkId.Value,
                    user!.Id,
                    rejectWhenProfilesMissing: false);

                string status = outcome.Success
                    ? "Success"
                    : outcome.Skipped ? "Skipped" : "Failed";

                return Json(new
                {
                    success = outcome.Success,
                    skipped = outcome.Skipped,
                    status,
                    serverId,
                    serverName = serverLabel,
                    message = outcome.Success
                        ? (outcome.SuccessMessage ?? "تم الاستيراد بنجاح")
                        : (outcome.ErrorMessage ?? "فشل الاستيراد"),
                    warnings = outcome.Warnings,
                    duplicateCount = outcome.DuplicateCount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في استيراد المشتركين (JSON) من السيرفر {ServerId}", serverId);
                return Json(new
                {
                    success = false,
                    skipped = true,
                    status = "Skipped",
                    serverId,
                    serverName = serverLabel,
                    message = BuildFriendlyMikroTikErrorMessage(
                        $"تعذر الاتصال بالسيرفر {serverLabel}. تم تخطيه والمتابعة",
                        ex)
                });
            }
        }

        // POST: Clients/QuickExtend - تمديد سريع لعدد أيام محدد
        [HttpPost]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> QuickExtend(int id, int days)
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
                ClientOperationOutcome outcome = await _app.Lifecycle.QuickExtendAsync(id, networkId.Value, days);
                if (outcome.IsSuccess)
                {
                    TempData["Success"] = $"✅ {outcome.SuccessMessage}";
                }
                else if (outcome.NotFound)
                {
                    return NotFound();
                }
                else
                {
                    TempData["Error"] = $"❌ {outcome.ErrorMessage}";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في التمديد", ex)}";
                _logger.LogError(ex, "خطأ في تمديد الاشتراك للعميل {ClientId}", id);
            }

            // العودة للصفحة السابقة
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction(nameof(Index));
        }

        // AJAX: جلب البروفايلات حسب الخادم من قاعدة البيانات
        public async Task<IActionResult> GetProfilesByServer(int serverId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!networkId.HasValue)
                {
                    return Json(Array.Empty<object>());
                }

                IReadOnlyList<ClientFormProfileOption> profiles =
                    await _app.FormLookup.GetProfilesByServerAsync(serverId, networkId.Value);
                return Json(profiles.Select(p => new { id = p.Id, name = p.Name }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب البروفايلات للخادم {ServerId}", serverId);
                return Json(Array.Empty<object>());
            }
        }

        // AJAX: جلب المستقبلات المرتبطة بمرسلات الخادم المحدد
        public async Task<IActionResult> GetReceiversByServer(int serverId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!networkId.HasValue)
                {
                    return Json(Array.Empty<object>());
                }

                IReadOnlyList<ClientFormReceiverOption> receivers =
                    await _app.FormLookup.GetReceiversByServerAsync(serverId, networkId.Value);
                return Json(receivers.Select(r => new { id = r.Id, name = r.Name, sectorName = r.SectorName }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب المستقبلات للخادم {ServerId}", serverId);
                return Json(Array.Empty<object>());
            }
        }
    }
}
