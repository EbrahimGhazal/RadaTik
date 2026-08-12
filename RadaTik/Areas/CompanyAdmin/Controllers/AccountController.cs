using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.ViewModels.Account;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _environment = environment;
    }

    /// <summary>
    /// الملف الشخصي لمدير الشركة (ضمن networkManager ليبقى في نفس المسار).
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        return View(await BuildProfileViewModelAsync(user));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            IList<string> roles = await _userManager.GetRolesAsync(user);
            model.Id = user.Id;
            model.UserName = user.UserName;
            model.Roles = roles.ToList();
            model.ShamCashQrCodePath = user.ShamCashQrCodePath;
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

        bool hasUpload = model.ShamCashQrCodeFile != null && model.ShamCashQrCodeFile.Length > 0;
        if (model.RemoveShamCashQrCode && hasUpload)
        {
            ModelState.AddModelError(nameof(model.RemoveShamCashQrCode), "لا يمكن حذف QR ورفع ملف جديد في نفس العملية.");
        }
        if (hasUpload && ImageUploadRules.IsTooLarge(model.ShamCashQrCodeFile))
        {
            ModelState.AddModelError(nameof(model.ShamCashQrCodeFile), ImageUploadRules.MaxQrImageSizeMessage);
        }

        if (model.RemoveShamCashQrCode && ModelState.IsValid)
        {
            DeleteQrIfExists(user.ShamCashQrCodePath);
            user.ShamCashQrCodePath = null;
            hasChanges = true;
        }
        else if (hasUpload && ModelState.IsValid)
        {
            string? savedPath = await SaveShamCashQrAsync(model.ShamCashQrCodeFile!);
            if (savedPath == null)
            {
                ModelState.AddModelError(nameof(model.ShamCashQrCodeFile), "يرجى رفع صورة QR بصيغة مقبولة (JPG, PNG, GIF, WebP).");
            }
            else
            {
                DeleteQrIfExists(user.ShamCashQrCodePath);
                user.ShamCashQrCodePath = savedPath;
                hasChanges = true;
            }
        }

        if (!ModelState.IsValid)
        {
            IList<string> roles = await _userManager.GetRolesAsync(user);
            model.Id = user.Id;
            model.UserName = user.UserName;
            model.Roles = roles.ToList();
            model.ShamCashQrCodePath = user.ShamCashQrCodePath;
            return View("Profile", model);
        }

        if (!hasChanges)
        {
            TempData["Info"] = "لم يتم إجراء أي تغيير على البيانات.";
            return await ReturnProfileViewAsync(user);
        }

        user.LastUpdated = DateTime.Now;
        IdentityResult result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "تعذر حفظ QR شام كاش.";
            return await ReturnProfileViewAsync(user);
        }

        TempData["Success"] = AppMessages.OperationSuccess;
        return await ReturnProfileViewAsync(user, saved: true);
    }

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
            ShamCashQrCodePath = user.ShamCashQrCodePath,
            Roles = roles.ToList()
        };
    }

    private async Task<IActionResult> ReturnProfileViewAsync(ApplicationUser user, bool saved = false)
    {
        if (saved)
        {
            ViewData["ShowSaveSuccess"] = true;
        }

        ApplicationUser? current = await _userManager.GetUserAsync(User) ?? user;
        return View("Profile", await BuildProfileViewModelAsync(current));
    }

    private IActionResult RedirectToProfile() =>
        RedirectToRoute("networkManager-account-profile");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            string? validationMessage = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault();
            TempData["Error"] = validationMessage ?? "بيانات تغيير كلمة المرور غير مكتملة.";
            return RedirectToProfile();
        }

        IdentityResult result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToProfile();
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = AppMessages.OperationSuccess;
        return Redirect("/networkManager/Account/profile?saved=1");
    }

    private async Task<string?> SaveShamCashQrAsync(IFormFile file)
    {
        if (ImageUploadRules.IsTooLarge(file))
        {
            return null;
        }

        string[] allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        string? ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !allowedExtensions.Contains(ext))
        {
            return null;
        }

        string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "shamcash-qr");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using FileStream stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/shamcash-qr/{uniqueFileName}";
    }

    private void DeleteQrIfExists(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return;
        }

        string normalized = publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(_environment.WebRootPath, normalized);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
