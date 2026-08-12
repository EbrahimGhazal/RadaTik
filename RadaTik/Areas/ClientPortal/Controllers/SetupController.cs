using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Areas.ClientPortal.ViewModels.Setup;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.ClientPortal.Controllers;

[Area("ClientPortal")]
[Authorize(Roles = RoleNames.Client)]
public class SetupController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<SetupController> logger) : Controller
{
    [HttpGet]
    public async Task<IActionResult> RequiredPassword()
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!user.MustChangePassword)
        {
            return RedirectToRoute("clientPortal-dashboard");
        }

        ViewData["Title"] = "تعيين كلمة مرور البوابة";
        return View(new ClientPortalRequiredPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequiredPassword(ClientPortalRequiredPasswordViewModel model)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!user.MustChangePassword)
        {
            return RedirectToRoute("clientPortal-dashboard");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "تعيين كلمة مرور البوابة";
            return View(model);
        }

        (bool success, IReadOnlyList<string> errors) = await ClientPortalAccountPasswordService.SetPortalPasswordAsync(
            userManager,
            user,
            model.NewPassword,
            completingRequiredChange: true);

        if (!success)
        {
            foreach (string error in errors)
            {
                ModelState.AddModelError(string.Empty, error);
            }

            ViewData["Title"] = "تعيين كلمة مرور البوابة";
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        logger.LogInformation(
            "Client portal user {UserId} changed portal password (MikroTik PPPoE password unchanged).",
            user.Id);

        TempData["Success"] = "تم تعيين كلمة مرور البوابة. لم يتم تغيير كلمة مرور الإنترنت (PPPoE) على المايكروتك.";
        return RedirectToRoute("clientPortal-dashboard");
    }
}
