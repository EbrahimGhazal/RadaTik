using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Helpers;
using RadTik.Areas.SystemAdmin.ViewModels.Account;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AccountController> _logger;
    private readonly IWebHostEnvironment _environment;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AccountController> logger,
        IWebHostEnvironment environment)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var vm = new SystemAdminProfileViewModel
        {
            UserName = user.UserName,
            Email = user.Email,
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            Address = user.Address,
            ShamCashQrCodePath = user.ShamCashQrCodePath,
            CreatedDate = user.CreatedDate
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(SystemAdminProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        // We allow updating a few personal fields only.
        // Username is intentionally read-only.
        if (!ModelState.IsValid)
        {
            // Preserve CreatedDate for display
            model.CreatedDate = user.CreatedDate;
            model.UserName = user.UserName;
            return View("Profile", model);
        }

        // Email (optional)
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
        }

        // Phone (optional)
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
        }

        if (model.RemoveShamCashQrCode)
        {
            DeleteQrIfExists(user.ShamCashQrCodePath);
            user.ShamCashQrCodePath = null;
        }
        else if (model.ShamCashQrCodeFile != null && model.ShamCashQrCodeFile.Length > 0)
        {
            if (ImageUploadRules.IsTooLarge(model.ShamCashQrCodeFile))
            {
                ModelState.AddModelError(nameof(model.ShamCashQrCodeFile), ImageUploadRules.MaxQrImageSizeMessage);
            }
            else
            {
                var savedPath = await SaveShamCashQrAsync(model.ShamCashQrCodeFile);
                if (savedPath == null)
                {
                    ModelState.AddModelError(nameof(model.ShamCashQrCodeFile), "يرجى رفع صورة QR بصيغة مقبولة (JPG, PNG, GIF, WebP).");
                }
                else
                {
                    DeleteQrIfExists(user.ShamCashQrCodePath);
                    user.ShamCashQrCodePath = savedPath;
                }
            }
        }

        // Keep name/address immutable from profile screen.
        // Only email, phone, and ShamCash QR are editable.
        user.LastUpdated = DateTime.Now;

        if (!ModelState.IsValid)
        {
            model.CreatedDate = user.CreatedDate;
            model.UserName = user.UserName;
            return View("Profile", model);
        }

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var err in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            model.CreatedDate = user.CreatedDate;
            model.UserName = user.UserName;
            return View("Profile", model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "تم تحديث بيانات الحساب بنجاح.";
        _logger.LogInformation("SystemAdmin profile updated for user {UserId}", user.Id);

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(SystemAdminChangePasswordViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        if (!ModelState.IsValid)
        {
            var profileVm = new SystemAdminProfileViewModel
            {
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                ShamCashQrCodePath = user.ShamCashQrCodePath,
                CreatedDate = user.CreatedDate
            };

            ViewBag.PasswordErrors = true;
            return View("Profile", profileVm);
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            var profileVm = new SystemAdminProfileViewModel
            {
                UserName = user.UserName,
                Email = user.Email,
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber,
                Address = user.Address,
                ShamCashQrCodePath = user.ShamCashQrCodePath,
                CreatedDate = user.CreatedDate
            };

            ViewBag.PasswordErrors = true;
            return View("Profile", profileVm);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "تم تغيير كلمة المرور بنجاح.";
        _logger.LogInformation("SystemAdmin password changed for user {UserId}", user.Id);

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

