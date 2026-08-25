using Microsoft.AspNetCore.Mvc;
using RadaTik.Constants;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.Documents;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

public sealed partial class ReportsController
{
    public sealed class DocumentAppearanceForm
    {
        public DocumentHeaderLayout HeaderLayout { get; set; }
        public bool ShowLogo { get; set; } = true;
        public bool UseNetworkLogo { get; set; } = true;
        public bool RemoveCustomLogo { get; set; }
        public string? PrimaryColor { get; set; }
        public string? TableHeaderColor { get; set; }
        public DocumentWatermarkMode WatermarkMode { get; set; }
        public string? WatermarkText { get; set; }
        public int WatermarkOpacityPercent { get; set; } = 12;
        public DocumentTableDensity TableDensity { get; set; }
        public bool StripedRows { get; set; } = true;
        public string? FooterText { get; set; }
        public bool ShowGeneratedAt { get; set; } = true;
        public IFormFile? LogoFile { get; set; }
    }

    [HttpGet]
    public async Task<IActionResult> DocumentAppearance()
    {
        ViewData["Title"] = "هوية مستندات الشركة";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        int? companyNetworkId = await _documentAppearance.ResolveCompanyNetworkIdAsync(selectedNetworkId.Value);
        if (!companyNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        CompanyDocumentAppearanceEditor editor = await _documentAppearance.GetEditorAsync(companyNetworkId.Value);
        ViewBag.Editor = editor;
        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_db, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;
        return View(editor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DocumentAppearance(DocumentAppearanceForm form)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        int? companyNetworkId = await _documentAppearance.ResolveCompanyNetworkIdAsync(selectedNetworkId.Value);
        if (!companyNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        if (form.LogoFile != null && ImageUploadRules.IsTooLarge(form.LogoFile))
        {
            TempData["Error"] = ImageUploadRules.MaxNetworkLogoSizeMessage;
            return RedirectToAction(nameof(DocumentAppearance));
        }

        await _documentAppearance.SaveAsync(
            companyNetworkId.Value,
            user.Id,
            new CompanyDocumentAppearanceSaveCommand
            {
                HeaderLayout = form.HeaderLayout,
                ShowLogo = form.ShowLogo,
                UseNetworkLogo = form.UseNetworkLogo,
                RemoveCustomLogo = form.RemoveCustomLogo,
                PrimaryColor = form.PrimaryColor,
                TableHeaderColor = form.TableHeaderColor,
                WatermarkMode = form.WatermarkMode,
                WatermarkText = form.WatermarkText,
                WatermarkOpacityPercent = form.WatermarkOpacityPercent,
                TableDensity = form.TableDensity,
                StripedRows = form.StripedRows,
                FooterText = form.FooterText,
                ShowGeneratedAt = form.ShowGeneratedAt,
                LogoFile = form.LogoFile
            });

        TempData["Success"] = "تم حفظ هوية مستندات شركتك. هذا التصميم خاص بشركتك ولا يظهر لدى الشركات الأخرى.";
        return RedirectToAction(nameof(DocumentAppearance));
    }
}
