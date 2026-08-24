using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Sectors;

namespace RadaTik.Controllers;

public partial class SectorController
{
    /// <summary>تنزيل قالب Excel لاستيراد المرسلات.</summary>
    [HttpGet]
    [RequirePermission("Sectors.Create")]
    public IActionResult DownloadSectorExcelImportTemplate([FromServices] ISectorExcelImportService importService)
    {
        byte[] bytes = importService.BuildTemplateWorkbook();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "قالب_استيراد_المرسلات.xlsx");
    }

    /// <summary>استيراد مرسلات جديدة من ملف Excel/CSV وإضافتها إلى قاعدة البيانات.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Sectors.Create")]
    public async Task<IActionResult> ImportFromExcel(
        IFormFile? file,
        [FromServices] ISectorExcelImportService importService)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
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
            SectorExcelImportParseResult parsed = await importService.ParseAsync(
                stream,
                file.FileName,
                networkId.Value);

            if (!parsed.Success)
            {
                TempData["Error"] = parsed.Message;
                StoreImportDetails(parsed.Details);
                return RedirectToAction(nameof(Index));
            }

            if (parsed.ImportableCount <= 0)
            {
                TempData["Error"] = parsed.Message;
                StoreImportDetails(parsed.Details);
                return RedirectToAction(nameof(Index));
            }

            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSector,
                parsed.ImportableCount);

            if (estimate.HasCharge && !estimate.HasSufficientBalance)
            {
                TempData["Error"] =
                    $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({estimate.WalletBalance:N2}) أقل من المبلغ المطلوب ({estimate.RequiredAmountSyp:N2}) ل.س.ج.";
                return RedirectToAction(nameof(Index));
            }

            SectorExcelImportResult result = await importService.CommitAsync(parsed.SectorsToAdd);
            if (result.AddedCount > 0 && user != null)
            {
                await _usageChargeService.ChargeUsageIncreaseAsync(
                    companyNetworkId,
                    user.Id,
                    PricingChargeUnit.PerSector);
            }

            string summary = $"{result.Message} تم تخطي {parsed.SkippedCount} وفشل {parsed.FailedCount}.";
            if (result.AddedCount > 0)
            {
                TempData["Success"] = summary;
            }
            else
            {
                TempData["Error"] = summary;
            }

            StoreImportDetails(parsed.Details);
        }
        catch (Exception)
        {
            TempData["Error"] = "تعذر استيراد الملف. تحقق من الصيغة والأعمدة ثم أعد المحاولة.";
        }

        return RedirectToAction(nameof(Index));
    }

    private void StoreImportDetails(List<string> details)
    {
        if (details.Count == 0)
        {
            return;
        }

        TempData["ImportSectorWarnings"] = string.Join(" | ", details.Take(20));
    }
}
