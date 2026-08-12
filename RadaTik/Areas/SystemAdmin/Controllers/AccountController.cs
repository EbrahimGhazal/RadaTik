using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Helpers;
using RadaTik.Areas.SystemAdmin.ViewModels.Account;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.SystemAdmin.Controllers;

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
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        SystemAdminProfileViewModel vm = new SystemAdminProfileViewModel
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
        ApplicationUser? user = await _userManager.GetUserAsync(User);
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
        }

        // Phone (optional)
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
                string? savedPath = await SaveShamCashQrAsync(model.ShamCashQrCodeFile);
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

        IdentityResult updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (IdentityError err in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            model.CreatedDate = user.CreatedDate;
            model.UserName = user.UserName;
            return View("Profile", model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = AppMessages.OperationSuccess;
        _logger.LogInformation("SystemAdmin profile updated for user {UserId}", user.Id);

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(SystemAdminChangePasswordViewModel model)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        foreach (string error in StrongPasswordRules.Validate(model.NewPassword, user.UserName, user.Email))
        {
            ModelState.AddModelError(nameof(model.NewPassword), error);
        }

        if (!ModelState.IsValid)
        {
            SystemAdminProfileViewModel profileVm = new SystemAdminProfileViewModel
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

        IdentityResult result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (IdentityError err in result.Errors)
            {
                ModelState.AddModelError(string.Empty, err.Description);
            }

            SystemAdminProfileViewModel profileVm = new SystemAdminProfileViewModel
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

        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = false;
        user.LastUpdated = DateTime.Now;
        await _userManager.UpdateAsync(user);

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = AppMessages.OperationSuccess;
        _logger.LogInformation("SystemAdmin password changed for user {UserId}", user.Id);

        return RedirectToAction(nameof(Profile));
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

