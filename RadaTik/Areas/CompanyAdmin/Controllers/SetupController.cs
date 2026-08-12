using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Areas.CompanyAdmin.ViewModels.Setup;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
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
            return RedirectToRoute("networkManager-dashboard");
        }

        ViewData["Title"] = "تعيين كلمة مرور جديدة";
        return View(new NetworkManagerRequiredPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequiredPassword(NetworkManagerRequiredPasswordViewModel model)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!user.MustChangePassword)
        {
            return RedirectToRoute("networkManager-dashboard");
        }

        foreach (string error in StrongPasswordRules.Validate(model.NewPassword, user.UserName, user.Email))
        {
            ModelState.AddModelError(nameof(model.NewPassword), error);
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "تعيين كلمة مرور جديدة";
            return View(model);
        }

        string resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult resetResult = await userManager.ResetPasswordAsync(user, resetToken, model.NewPassword);
        if (!resetResult.Succeeded)
        {
            foreach (IdentityError err in resetResult.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            ViewData["Title"] = "تعيين كلمة مرور جديدة";
            return View(model);
        }

        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        user.LastUpdated = DateTime.Now;
        IdentityResult updateResult = await userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (IdentityError err in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            ViewData["Title"] = "تعيين كلمة مرور جديدة";
            return View(model);
        }

        await signInManager.RefreshSignInAsync(user);
        logger.LogInformation("Network manager {UserId} completed required password change.", user.Id);
        TempData["Success"] = "تم تعيين كلمة المرور بنجاح. يمكنك متابعة استخدام النظام.";

        return RedirectToRoute("networkManager-dashboard");
    }
}
