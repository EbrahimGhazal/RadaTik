using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Areas.SystemAdmin.ViewModels.Setup;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class SetupController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ISystemAdminPricingReadinessService pricingReadiness,
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
            return await RedirectToNextSetupStepAsync();
        }

        ViewData["Title"] = "تعيين كلمة مرور جديدة";
        return View(new SystemAdminRequiredPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequiredPassword(SystemAdminRequiredPasswordViewModel model)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!user.MustChangePassword)
        {
            return await RedirectToNextSetupStepAsync();
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
        logger.LogInformation("System admin {UserId} completed required password change.", user.Id);
        TempData["Success"] = "تم تعيين كلمة المرور بنجاح. أكمل تهيئة أسعار الخدمات للمتابعة.";

        return await RedirectToNextSetupStepAsync();
    }

    [HttpGet]
    public async Task<IActionResult> Pricing()
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (user.MustChangePassword)
        {
            return RedirectToAction(nameof(RequiredPassword));
        }

        SystemAdminPricingReadiness readiness = await pricingReadiness.EvaluateAsync();
        if (readiness.IsComplete)
        {
            TempData["Success"] = "تهيئة أسعار الخدمات مكتملة. يمكنك استخدام النظام الآن.";
            return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
        }

        ViewData["Title"] = "تهيئة أسعار الخدمات";
        return View(readiness);
    }

    private async Task<IActionResult> RedirectToNextSetupStepAsync()
    {
        SystemAdminPricingReadiness readiness = await pricingReadiness.EvaluateAsync();
        if (!readiness.IsComplete)
        {
            return RedirectToAction(nameof(Pricing));
        }

        return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
    }
}
