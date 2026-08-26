using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.ViewModels.Account;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager)
    {
        _userManager = userManager;
        _signInManager = signInManager;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        ViewData["Title"] = "بروفايلي";
        return View(await BuildProfileViewModelAsync(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        model.Id = user.Id;
        model.UserName = user.UserName;
        model.Roles = (await _userManager.GetRolesAsync(user)).ToList();
        model.ShamCashQrCodePath = null;
        model.ShamCashQrCodeFile = null;
        model.RemoveShamCashQrCode = false;

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "بروفايلي";
            return View("Profile", model);
        }

        bool hasChanges = false;

        string? requestedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        if (!string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase))
        {
            IdentityResult emailResult = await _userManager.SetEmailAsync(user, requestedEmail);
            if (!emailResult.Succeeded)
            {
                foreach (IdentityError err in emailResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.Email), err.Description);
                }
            }
            else
            {
                hasChanges = true;
            }
        }

        string? requestedPhone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        if (!string.Equals(user.PhoneNumber, requestedPhone, StringComparison.OrdinalIgnoreCase))
        {
            IdentityResult phoneResult = await _userManager.SetPhoneNumberAsync(user, requestedPhone);
            if (!phoneResult.Succeeded)
            {
                foreach (IdentityError err in phoneResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), err.Description);
                }
            }
            else
            {
                hasChanges = true;
            }
        }

        string? requestedFullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim();
        if (!string.Equals(user.FullName, requestedFullName, StringComparison.Ordinal))
        {
            user.FullName = requestedFullName;
            hasChanges = true;
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "بروفايلي";
            return View("Profile", model);
        }

        if (!hasChanges)
        {
            TempData["Info"] = "لم يتم إجراء أي تغيير.";
            return RedirectToEmployeeProfile();
        }

        user.LastUpdated = DateTime.Now;
        IdentityResult result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToEmployeeProfile();
        }

        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToEmployeeProfile();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault()
                ?? "بيانات تغيير كلمة المرور غير مكتملة.";
            return RedirectToEmployeeProfile();
        }

        foreach (string err in StrongPasswordRules.Validate(model.NewPassword, user.UserName, user.Email))
        {
            TempData["Error"] = err;
            return RedirectToEmployeeProfile();
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToEmployeeProfile();
        }

        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);
        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
        return RedirectToEmployeeProfile();
    }

    private RedirectToRouteResult RedirectToEmployeeProfile() =>
        RedirectToRoute("employee-profile");

    private async Task<ProfileViewModel> BuildProfileViewModelAsync(ApplicationUser user)
    {
        IList<string> roles = await _userManager.GetRolesAsync(user);
        return new ProfileViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Roles = roles.ToList()
        };
    }
}
