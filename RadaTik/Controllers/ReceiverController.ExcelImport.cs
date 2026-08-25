using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Receivers;

namespace RadaTik.Controllers;

public partial class ReceiverController
{
    [HttpGet]
    [RequirePermission("Receivers.Create")]
    public IActionResult DownloadReceiverExcelImportTemplate([FromServices] IReceiverExcelImportService importService)
    {
        byte[] bytes = importService.BuildTemplateWorkbook();
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "قالب_استيراد_المستقبلات.xlsx");
    }

    [HttpGet]
    [RequirePermission("Receivers.View")]
    public async Task<IActionResult> ExportToExcel([FromServices] IReceiverExcelImportService importService)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        byte[] bytes = await importService.BuildExportWorkbookAsync(networkId.Value);
        return File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"المستقبلات_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequirePermission("Receivers.Create")]
    public async Task<IActionResult> ImportFromExcel(
        IFormFile? file,
        [FromServices] IReceiverExcelImportService importService)
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
            ReceiverExcelImportParseResult parsed = await importService.ParseAsync(
                stream,
                file.FileName,
                networkId.Value);

            if (!parsed.Success || parsed.ImportableCount <= 0)
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
                PricingChargeUnit.PerReceiver,
                parsed.ImportableCount);

            if (estimate.HasCharge && !estimate.HasSufficientBalance)
            {
                TempData["Error"] =
                    $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({estimate.WalletBalance:N2}) أقل من المبلغ المطلوب ({estimate.RequiredAmountSyp:N2}) ل.س.ج.";
                return RedirectToAction(nameof(Index));
            }

            ReceiverExcelImportResult result = await importService.CommitAsync(parsed.ReceiversToAdd);
            if (result.AddedCount > 0 && user != null)
            {
                await _usageChargeService.ChargeUsageIncreaseAsync(
                    companyNetworkId,
                    user.Id,
                    PricingChargeUnit.PerReceiver);
            }

            string summary = $"{result.Message} تم تخطي {parsed.SkippedCount} وفشل {parsed.FailedCount}.";
            TempData[result.AddedCount > 0 ? "Success" : "Error"] = summary;
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

        TempData["ImportReceiverWarnings"] = string.Join(" | ", details.Take(20));
    }
}
