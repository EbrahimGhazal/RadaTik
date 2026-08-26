using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Areas.ClientPortal.ViewModels.Account;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.ClientPortal.Controllers;

[Area("ClientPortal")]
[Authorize(Roles = RoleNames.Client)]
public class AccountController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ILogger<AccountController> logger) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ClientPortalChangePasswordViewModel model)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault()
                ?? "بيانات تغيير كلمة المرور غير مكتملة.";
            return RedirectToRoute("clientPortal-actions", new { action = "MyProfile" });
        }

        (bool success, IReadOnlyList<string> errors) = await ClientPortalAccountPasswordService.ChangePortalPasswordAsync(
            userManager,
            user,
            model.CurrentPassword,
            model.NewPassword);

        if (!success)
        {
            TempData["Error"] = errors.FirstOrDefault() ?? "تعذر تغيير كلمة مرور النظام.";
            return RedirectToRoute("clientPortal-actions", new { action = "MyProfile" });
        }

        await signInManager.RefreshSignInAsync(user);
        logger.LogInformation(
            "Client portal user {UserId} changed system password from profile (MikroTik PPPoE password unchanged).",
            user.Id);

        TempData["Success"] = "تم تغيير كلمة مرور النظام. لم يتم تغيير اسم المستخدم أو كلمة المرور على المايكروتك.";
        return RedirectToRoute("clientPortal-actions", new { action = "MyProfile" });
    }
}
