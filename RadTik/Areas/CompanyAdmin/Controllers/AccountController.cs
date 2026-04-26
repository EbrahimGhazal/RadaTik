using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.Account;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
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
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var model = new ProfileViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            ShamCashQrCodePath = user.ShamCashQrCodePath,
            Roles = roles.ToList()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            var roles = await _userManager.GetRolesAsync(user);
            model.Id = user.Id;
            model.UserName = user.UserName;
            model.Roles = roles.ToList();
            model.ShamCashQrCodePath = user.ShamCashQrCodePath;
            return View("Profile", model);
        }

        var hasChanges = false;

        var requestedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
        if (!string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await _userManager.SetEmailAsync(user, requestedEmail);
            if (!emailResult.Succeeded)
            {
                foreach (var err in emailResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.Email), err.Description);
                }
            }
            else
            {
                hasChanges = true;
            }
        }

        var requestedPhone = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
        if (!string.Equals(user.PhoneNumber, requestedPhone, StringComparison.OrdinalIgnoreCase))
        {
            var phoneResult = await _userManager.SetPhoneNumberAsync(user, requestedPhone);
            if (!phoneResult.Succeeded)
            {
                foreach (var err in phoneResult.Errors)
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), err.Description);
                }
            }
            else
            {
                hasChanges = true;
            }
        }

        var hasUpload = model.ShamCashQrCodeFile != null && model.ShamCashQrCodeFile.Length > 0;
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
            var savedPath = await SaveShamCashQrAsync(model.ShamCashQrCodeFile!);
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
            var roles = await _userManager.GetRolesAsync(user);
            model.Id = user.Id;
            model.UserName = user.UserName;
            model.Roles = roles.ToList();
            model.ShamCashQrCodePath = user.ShamCashQrCodePath;
            return View("Profile", model);
        }

        if (!hasChanges)
        {
            TempData["Info"] = "لم يتم إجراء أي تغيير على البيانات.";
            return RedirectToAction(nameof(Profile));
        }

        user.LastUpdated = DateTime.Now;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "تعذر حفظ QR شام كاش.";
            return RedirectToAction(nameof(Profile));
        }

        TempData["Success"] = "تم تحديث البيانات الشخصية بنجاح.";

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            var validationMessage = ModelState.Values
                .SelectMany(v => v.Errors)
                .Select(e => e.ErrorMessage)
                .FirstOrDefault();
            TempData["Error"] = validationMessage ?? "بيانات تغيير كلمة المرور غير مكتملة.";
            return RedirectToAction(nameof(Profile));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" | ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Profile));
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
        return RedirectToAction(nameof(Profile));
    }

    private async Task<string?> SaveShamCashQrAsync(IFormFile file)
    {
        if (ImageUploadRules.IsTooLarge(file))
        {
            return null;
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(ext) || !allowedExtensions.Contains(ext))
        {
            return null;
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "shamcash-qr");
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);
        return $"/uploads/shamcash-qr/{uniqueFileName}";
    }

    private void DeleteQrIfExists(string? publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath))
        {
            return;
        }

        var normalized = publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, normalized);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
