using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Clients;

namespace RadaTik.Controllers
{
    public partial class ClientsController : Controller
    {
        // GET: Clients/MembershipContract/5
        [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
        public async Task<IActionResult> MembershipContract(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            bool canView = await _app.Permission.HasPermissionAsync(User, "Clients.View");
            if (!canView && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                return Forbid();
            }

            int? restrictNetworkId = null;
            if (User.IsInRole(RoleNames.NetworkAdministrator) && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                restrictNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!restrictNetworkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }
            }

            ClientMembershipContractPageResult page = await _app.Contract.BuildMembershipContractPageAsync(
                id,
                restrictNetworkId);

            return page.Status switch
            {
                ClientContractPageStatus.NotFound => NotFound(),
                ClientContractPageStatus.RenewalBlocked => RedirectRenewalBlocked(page.ErrorMessage, id),
                _ => BindMembershipContractView(page)
            };
        }

        [HttpGet]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        public async Task<IActionResult> ContractTemplateSettings()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            Network? network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
            if (network == null)
            {
                TempData["Error"] = "تعذر العثور على الشبكة الحالية.";
                return RedirectToAction(nameof(Index));
            }

            ApplyContractTemplateSettingsViewData(await _app.Contract.BuildSettingsPageAsync(network.Id));
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        public async Task<IActionResult> ContractTemplateSettings(
            string contractTitle,
            string? recordNumber,
            string? licenseNumber,
            string contractBodyTemplate)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            Network? network = await _context.Networks.FirstOrDefaultAsync(n => n.Id == networkId.Value);
            if (network == null)
            {
                TempData["Error"] = "تعذر العثور على الشبكة الحالية.";
                return RedirectToAction(nameof(Index));
            }

            ClientContractSettingsSaveCommand command = new()
            {
                ContractTitle = contractTitle,
                RecordNumber = recordNumber,
                LicenseNumber = licenseNumber,
                ContractBodyTemplate = contractBodyTemplate
            };

            if (string.IsNullOrWhiteSpace(command.ContractTitle))
            {
                ModelState.AddModelError("contractTitle", "عنوان العقد مطلوب.");
            }

            if (string.IsNullOrWhiteSpace(command.ContractBodyTemplate))
            {
                ModelState.AddModelError("contractBodyTemplate", "نص العقد مطلوب.");
            }

            IReadOnlyList<string> unknownVariables = _app.Contract.ValidateTemplateVariables(command.ContractBodyTemplate);
            if (unknownVariables.Count > 0)
            {
                ModelState.AddModelError(
                    "contractBodyTemplate",
                    $"يوجد متغيرات غير معروفة داخل النص: {string.Join(", ", unknownVariables)}");
            }

            if (!ModelState.IsValid)
            {
                ClientContractSettingsSaveResult invalid = _app.Contract.ValidateSettingsSave(network, command);
                ApplyContractTemplateSettingsViewData(invalid.InvalidView!);
                return View();
            }

            await _app.Contract.SaveSettingsAsync(network.Id, command);
            TempData["Success"] = "تم حفظ إعدادات عقد الانضمام بنجاح.";
            return RedirectToAction(nameof(ContractTemplateSettings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        public async Task<IActionResult> ResetContractTemplateToDefault()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            await _app.Contract.ResetTemplateToDefaultAsync(networkId.Value);
            TempData["Success"] = "تمت إعادة ضبط نص العقد إلى القالب الافتراضي.";
            return RedirectToAction(nameof(ContractTemplateSettings));
        }

        private IActionResult BindMembershipContractView(ClientMembershipContractPageResult page)
        {
            ApplyContractPrintViewData(page.PrintView!);
            return View(page.Client);
        }

        private IActionResult RedirectRenewalBlocked(string? message, int clientId)
        {
            TempData["Error"] = message;
            return RedirectToAction(nameof(Details), new { id = clientId });
        }
    }
}
