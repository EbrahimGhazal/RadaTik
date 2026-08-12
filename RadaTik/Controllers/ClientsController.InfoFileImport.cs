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
        /// <summary>تنزيل قالب Excel لاستيراد معلومات المشتركين.</summary>
        [HttpGet]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
        [RequirePermission("Clients.Edit")]
        public IActionResult DownloadClientInfoImportTemplate()
        {
            byte[] bytes = _app.InfoFileImport.BuildTemplateWorkbook();
            return File(
                bytes,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                "قالب_استيراد_معلومات_المشتركين.xlsx");
        }

        /// <summary>استيراد معلومات المشتركين من ملف Excel/CSV وتحديث الحقول المتوفرة فقط.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> ImportClientInfoFromFile(IFormFile? file)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            if (file == null || file.Length == 0)
            {
                TempData["Error"] = "الرجاء اختيار ملف Excel أو CSV.";
                return RedirectToAction(nameof(Index));
            }

            string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (ext is not (".xlsx" or ".xlsm" or ".csv" or ".txt"))
            {
                TempData["Error"] = "صيغة الملف غير مدعومة. استخدم .xlsx أو .csv";
                return RedirectToAction(nameof(Index));
            }

            if (file.Length > 10 * 1024 * 1024)
            {
                TempData["Error"] = "حجم الملف كبير جداً (الحد الأقصى 10 ميجابايت).";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                await using Stream stream = file.OpenReadStream();
                ClientInfoFileImportResult result = await _app.InfoFileImport.ImportAsync(
                    stream,
                    file.FileName,
                    networkId.Value);

                if (result.UpdatedCount > 0)
                {
                    TempData["Success"] = result.Message;
                }
                else if (result.FailedCount > 0)
                {
                    TempData["Error"] = result.Message;
                }
                else
                {
                    TempData["Info"] = result.Message;
                }

                if (result.Details.Count > 0)
                {
                    TempData["ClientInfoImportDetails"] = string.Join(" | ", result.Details.Take(20));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في استيراد معلومات المشتركين من ملف");
                TempData["Error"] = "تعذر استيراد الملف. تحقق من الصيغة والمحاولة مرة أخرى.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
