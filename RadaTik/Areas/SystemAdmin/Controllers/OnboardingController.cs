using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class OnboardingController(
    UserManager<ApplicationUser> userManager,
    IOnboardingChecklistService onboardingService) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!await onboardingService.CanDismissSystemAsync(user.Id, cancellationToken))
        {
            TempData["Error"] = "أكمل تعيين كلمة المرور وتهيئة أسعار الخدمات قبل إخفاء دليل البدء.";
            return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
        }

        await onboardingService.DismissSystemAsync(user.Id, cancellationToken);
        TempData["Success"] = "تم إخفاء دليل البدء.";
        return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
    }
}
