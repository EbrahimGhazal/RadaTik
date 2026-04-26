using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.CollectionPoint.Controllers;

[Area("CollectionPoint")]
[Authorize(Roles = $"{RoleNames.CollectionPoint},{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
public class WalletController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WalletController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly RequestNotificationService _requestNotificationService;

    public WalletController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<WalletController> logger,
        IWebHostEnvironment environment,
        RequestNotificationService requestNotificationService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _environment = environment;
        _requestNotificationService = requestNotificationService;
    }

    /// <summary>
    /// طلب تغذية رصيد المحفظة - صفحة تقديم الطلب
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TopUp()
    {
        ViewData["Title"] = "طلب تغذية رصيد المحفظة";

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        ViewBag.CollectionPointShamCashQrCodePath = user.ShamCashQrCodePath;

        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            var acc = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (acc?.NetworkId != null)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, acc.NetworkId.Value);
                networkId = acc.NetworkId;
            }
        }

        if (!networkId.HasValue)
        {
            TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
            return View();
        }

        var account = await _context.CollectionPointAccounts
            .Include(a => a.Network)
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = user.Id,
                NetworkId = networkId.Value,
                Balance = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.CollectionPointAccounts.Add(account);
            await _context.SaveChangesAsync();
            account = await _context.CollectionPointAccounts
                .Include(a => a.Network)
                .FirstAsync(a => a.UserId == user.Id);
        }

        ViewBag.AccountBalance = account.Balance;
        ViewBag.NetworkName = account.Network?.Name;
        ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
            .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);

        // قائمة الشبكات التي تنتمي إليها نقطة التحصيل (للطلب من مدير الشركة)
        var networkIds = await _context.CollectionPointAccounts
            .Where(a => a.UserId == user.Id && a.NetworkId != null)
            .Select(a => a.NetworkId!.Value)
            .Distinct()
            .ToListAsync();
        var networksForRequest = await _context.Networks
            .Where(n => networkIds.Contains(n.Id))
            .Include(n => n.ManagerUser)
            .ToListAsync();
        ViewBag.NetworksForRequest = networksForRequest;

        ViewBag.PaymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();

        return View();
    }

    /// <summary>
    /// تقديم طلب تغذية رصيد
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp(
        decimal amount,
        string? notes,
        CollectionPointTopUpTarget requestTargetType,
        int? targetNetworkId,
        int? paymentMethodId,
        string? method,
        string? referenceNumber,
        IFormFile? receiptImage)
    {
        ViewData["Title"] = "طلب تغذية رصيد المحفظة";

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        ViewBag.CollectionPointShamCashQrCodePath = user.ShamCashQrCodePath;

        var paymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
        ViewBag.PaymentMethods = paymentMethods;

        async Task PopulateNetworksAsync()
        {
            var nids = await _context.CollectionPointAccounts
                .Where(a => a.UserId == user.Id && a.NetworkId != null)
                .Select(a => a.NetworkId!.Value)
                .Distinct()
                .ToListAsync();
            ViewBag.NetworksForRequest = await _context.Networks
                .Where(n => nids.Contains(n.Id))
                .Include(n => n.ManagerUser)
                .ToListAsync();
        }

        var account = await _context.CollectionPointAccounts
            .Include(a => a.Network)
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        if (account == null || account.NetworkId == null)
        {
            TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
            ViewBag.AccountBalance = 0m;
            ViewBag.NetworkName = "";
            ViewBag.PendingCount = 0;
            await PopulateNetworksAsync();
            return View();
        }

        if (amount < 0.01m)
        {
            TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
            ViewBag.AccountBalance = account.Balance;
            ViewBag.NetworkName = account.Network?.Name;
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
            await PopulateNetworksAsync();
            return View();
        }

        if (requestTargetType == CollectionPointTopUpTarget.CompanyManager && (!targetNetworkId.HasValue || targetNetworkId == 0))
        {
            TempData["Error"] = "يرجى تحديد صاحب الشركة أو الشبكة عند اختيار مدير الشركة.";
            ViewBag.AccountBalance = account.Balance;
            ViewBag.NetworkName = account.Network?.Name;
            await PopulateNetworksAsync();
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
            return View();
        }

        PaymentMethod? pm = null;
        if (paymentMethods.Count > 0)
        {
            if (!paymentMethodId.HasValue)
            {
                TempData["Error"] = "يرجى اختيار طريقة الدفع.";
                ViewBag.AccountBalance = account.Balance;
                ViewBag.NetworkName = account.Network?.Name;
                await PopulateNetworksAsync();
                ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                    .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
                return View();
            }

            pm = paymentMethods.FirstOrDefault(x => x.Id == paymentMethodId.Value);
            if (pm == null)
            {
                TempData["Error"] = "طريقة الدفع غير صالحة.";
                ViewBag.AccountBalance = account.Balance;
                ViewBag.NetworkName = account.Network?.Name;
                await PopulateNetworksAsync();
                ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                    .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
                return View();
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                TempData["Error"] = "يرجى إدخال طريقة الدفع.";
                ViewBag.AccountBalance = account.Balance;
                ViewBag.NetworkName = account.Network?.Name;
                await PopulateNetworksAsync();
                ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                    .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
                return View();
            }
        }

        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            TempData["Error"] = "يرجى إدخال رقم الإشعار/المرجع.";
            ViewBag.AccountBalance = account.Balance;
            ViewBag.NetworkName = account.Network?.Name;
            await PopulateNetworksAsync();
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
            return View();
        }

        if (receiptImage == null || receiptImage.Length == 0)
        {
            TempData["Error"] = "يرجى رفع صورة الإيصال.";
            ViewBag.AccountBalance = account.Balance;
            ViewBag.NetworkName = account.Network?.Name;
            await PopulateNetworksAsync();
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
            return View();
        }
        if (ImageUploadRules.IsTooLarge(receiptImage))
        {
            TempData["Error"] = ImageUploadRules.MaxReceiptImageSizeMessage;
            ViewBag.AccountBalance = account.Balance;
            ViewBag.NetworkName = account.Network?.Name;
            await PopulateNetworksAsync();
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
            return View();
        }

        string? receiptPath = null;
        if (receiptImage.Length > 0)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(receiptImage.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "يرجى رفع صورة بصيغة مقبولة (JPG, PNG, GIF, WebP).";
                ViewBag.AccountBalance = account.Balance;
                ViewBag.NetworkName = account.Network?.Name;
                await PopulateNetworksAsync();
                ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                    .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
                return View();
            }

            var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);
            var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(uploadsFolder, uniqueFileName);
            await using (var stream = new FileStream(filePath, FileMode.Create))
                await receiptImage.CopyToAsync(stream);
            receiptPath = $"/uploads/receipts/{uniqueFileName}";
        }

        var request = new CollectionPointTopUpRequest
        {
            CollectionPointAccountId = account.Id,
            RequestTargetType = requestTargetType,
            TargetNetworkId = requestTargetType == CollectionPointTopUpTarget.CompanyManager ? targetNetworkId : null,
            Amount = amount,
            PaymentMethodId = pm?.Id,
            Method = pm?.Name ?? (string.IsNullOrWhiteSpace(method) ? null : method.Trim()),
            ReferenceNumber = referenceNumber.Trim(),
            ReceiptImagePath = receiptPath,
            Notes = notes?.Trim(),
            Status = CollectionPointTopUpStatus.Pending,
            RequestedByUserId = user.Id,
            RequestedAt = DateTime.Now
        };

        _context.CollectionPointTopUpRequests.Add(request);
        await _context.SaveChangesAsync();

        await _requestNotificationService.NotifyCollectionPointTopUpRequestSubmittedAsync(
            request,
            user.FullName ?? user.UserName);

        _logger.LogInformation("تم تقديم طلب تغذية رصيد #{RequestId} من نقطة التحصيل {UserId} بمبلغ {Amount}",
            request.Id, user.Id, amount);

        TempData["Success"] = requestTargetType == CollectionPointTopUpTarget.SystemAdmin
            ? "تم إرسال طلب تغذية الرصيد بنجاح. سيتم مراجعته من مدير النظام."
            : "تم إرسال طلب تغذية الرصيد بنجاح. سيتم مراجعته من مدير الشركة.";
        return RedirectToAction(nameof(TopUp));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateShamCashQr(IFormFile? shamCashQrCodeFile, bool removeShamCashQrCode = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var hasUpload = shamCashQrCodeFile != null && shamCashQrCodeFile.Length > 0;
        if (removeShamCashQrCode && hasUpload)
        {
            TempData["Error"] = "لا يمكن حذف QR ورفع ملف جديد في نفس العملية.";
            return RedirectToAction(nameof(TopUp));
        }

        if (removeShamCashQrCode)
        {
            DeleteQrIfExists(user.ShamCashQrCodePath);
            user.ShamCashQrCodePath = null;
        }
        else if (hasUpload)
        {
            if (ImageUploadRules.IsTooLarge(shamCashQrCodeFile))
            {
                TempData["Error"] = ImageUploadRules.MaxQrImageSizeMessage;
                return RedirectToAction(nameof(TopUp));
            }

            var savedPath = await SaveShamCashQrAsync(shamCashQrCodeFile!);
            if (savedPath == null)
            {
                TempData["Error"] = "يرجى رفع صورة QR بصيغة مقبولة (JPG, PNG, GIF, WebP).";
                return RedirectToAction(nameof(TopUp));
            }

            DeleteQrIfExists(user.ShamCashQrCodePath);
            user.ShamCashQrCodePath = savedPath;
        }
        else
        {
            TempData["Info"] = "لم يتم إجراء أي تغيير على QR شام كاش.";
            return RedirectToAction(nameof(TopUp));
        }

        user.LastUpdated = DateTime.Now;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "تعذر حفظ QR شام كاش.";
            return RedirectToAction(nameof(TopUp));
        }

        TempData["Success"] = removeShamCashQrCode
            ? "تم حذف QR شام كاش بنجاح."
            : "تم تحديث QR شام كاش بنجاح.";
        return RedirectToAction(nameof(TopUp));
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
