using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CollectionPoint.Controllers;

[Area("CollectionPoint")]
[Authorize(Roles = $"{RoleNames.CollectionPoint},{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
public class WalletController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WalletController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IRequestNotificationService _requestNotificationService;
    private readonly ClientWalletTopUpApprovalService _clientTopUpApprovalService;

    public WalletController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<WalletController> logger,
        IWebHostEnvironment environment,
        IRequestNotificationService requestNotificationService,
        ClientWalletTopUpApprovalService clientTopUpApprovalService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _environment = environment;
        _requestNotificationService = requestNotificationService;
        _clientTopUpApprovalService = clientTopUpApprovalService;
    }

    /// <summary>
    /// طلب تغذية رصيد المحفظة - صفحة تقديم الطلب
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> TopUp()
    {
        ViewData["Title"] = "طلب تغذية رصيد المحفظة";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        ViewBag.CollectionPointShamCashQrCodePath = user.ShamCashQrCodePath;

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            CollectionPointAccount? acc = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (acc?.NetworkId != null)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, acc.NetworkId.Value);
                networkId = acc.NetworkId;
            }
        }

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .Include(a => a.Network)
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = user.Id,
                NetworkId = networkId,
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }
        ViewBag.CollectionPointShamCashQrCodePath = user.ShamCashQrCodePath;

        List<PaymentMethod> paymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
        ViewBag.PaymentMethods = paymentMethods;

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .Include(a => a.Network)
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        async Task ReturnViewWithAccountStateAsync()
        {
            ViewBag.AccountBalance = account!.Balance;
            ViewBag.NetworkName = account.Network?.Name;
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.RequestedByUserId == user.Id && r.Status == CollectionPointTopUpStatus.Pending);
        }

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = user.Id,
                NetworkId = null,
                Balance = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.CollectionPointAccounts.Add(account);
            await _context.SaveChangesAsync();

            ViewBag.AccountBalance = 0m;
            ViewBag.NetworkName = "";
            ViewBag.PendingCount = 0;
            return View();
        }

        // طلبات تغذية رصيد نقطة التحصيل تُوجَّه حصرياً إلى مدير النظام (لا يُقبل تلاعب بقيم النموذج).
        requestTargetType = CollectionPointTopUpTarget.SystemAdmin;
        targetNetworkId = null;

        if (amount < 0.01m)
        {
            TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
            await ReturnViewWithAccountStateAsync();
            return View();
        }

        PaymentMethod? pm = null;
        if (paymentMethods.Count > 0)
        {
            if (!paymentMethodId.HasValue)
            {
                TempData["Error"] = "يرجى اختيار طريقة الدفع.";
                await ReturnViewWithAccountStateAsync();
                return View();
            }

            pm = paymentMethods.FirstOrDefault(x => x.Id == paymentMethodId.Value);
            if (pm == null)
            {
                TempData["Error"] = "طريقة الدفع غير صالحة.";
                await ReturnViewWithAccountStateAsync();
                return View();
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                TempData["Error"] = "يرجى إدخال طريقة الدفع.";
                await ReturnViewWithAccountStateAsync();
                return View();
            }
        }

        if (string.IsNullOrWhiteSpace(referenceNumber))
        {
            TempData["Error"] = "يرجى إدخال رقم الإشعار/المرجع.";
            await ReturnViewWithAccountStateAsync();
            return View();
        }

        if (receiptImage == null || receiptImage.Length == 0)
        {
            TempData["Error"] = "يرجى رفع صورة الإيصال.";
            await ReturnViewWithAccountStateAsync();
            return View();
        }
        if (ImageUploadRules.IsTooLarge(receiptImage))
        {
            TempData["Error"] = ImageUploadRules.MaxReceiptImageSizeMessage;
            await ReturnViewWithAccountStateAsync();
            return View();
        }

        string? receiptPath = null;
        if (receiptImage.Length > 0)
        {
            string[] allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            string? ext = Path.GetExtension(receiptImage.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
            {
                TempData["Error"] = "يرجى رفع صورة بصيغة مقبولة (JPG, PNG, GIF, WebP).";
                await ReturnViewWithAccountStateAsync();
                return View();
            }

            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            await using (FileStream stream = new FileStream(filePath, FileMode.Create))
            {
                await receiptImage.CopyToAsync(stream);
            }

            receiptPath = $"/uploads/receipts/{uniqueFileName}";
        }

        CollectionPointTopUpRequest request = new CollectionPointTopUpRequest
        {
            CollectionPointAccountId = account.Id,
            RequestTargetType = CollectionPointTopUpTarget.SystemAdmin,
            TargetNetworkId = null,
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

        TempData["Success"] = "تم إرسال طلب تغذية الرصيد بنجاح. سيتم مراجعته من مدير النظام.";
        return RedirectToAction(nameof(TopUp));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateShamCashQr(IFormFile? shamCashQrCodeFile, bool removeShamCashQrCode = false)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        bool hasUpload = shamCashQrCodeFile != null && shamCashQrCodeFile.Length > 0;
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

            string? savedPath = await SaveShamCashQrAsync(shamCashQrCodeFile!);
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
        IdentityResult result = await _userManager.UpdateAsync(user);
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

    /// <summary>طلبات تغذية المشتركين الموجّهة لنقطة التحصيل.</summary>
    [HttpGet]
    public async Task<IActionResult> ClientTopUpRequests(ClientWalletTopUpRequestStatus? status = null)
    {
        ViewData["Title"] = "طلبات تغذية المشتركين";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id);
        if (account == null)
        {
            TempData["Error"] = "حساب نقطة التحصيل غير موجود.";
            return RedirectToAction(nameof(TopUp));
        }

        IQueryable<ClientWalletTopUpRequest> query = _context.ClientWalletTopUpRequests
            .AsNoTracking()
            .Include(r => r.Client)
            .Include(r => r.PaymentMethod)
            .Where(r =>
                r.RecipientTarget == ClientWalletTopUpRecipientTarget.CollectionPoint &&
                r.TargetCollectionPointAccountId == account.Id);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        List<ClientWalletTopUpRequest> items = await query
            .OrderByDescending(r => r.RequestedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.SelectedStatus = status;
        ViewBag.PendingCount = await _context.ClientWalletTopUpRequests.CountAsync(r =>
            r.TargetCollectionPointAccountId == account.Id &&
            r.RecipientTarget == ClientWalletTopUpRecipientTarget.CollectionPoint &&
            r.Status == ClientWalletTopUpRequestStatus.Pending);
        ViewBag.AccountBalance = account.Balance;

        return View("ClientTopUpRequests", items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveClientTopUp(int id, string? adminNotes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id);
        if (account == null)
        {
            TempData["Error"] = "حساب نقطة التحصيل غير موجود.";
            return RedirectToAction(nameof(ClientTopUpRequests));
        }

        ClientWalletTopUpApprovalResult result = await _clientTopUpApprovalService.ApproveAsync(
            id,
            user.Id,
            ClientWalletTopUpRecipientTarget.CollectionPoint,
            account.Id,
            adminNotes);

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "تمت الموافقة وإضافة الرصيد لمحفظة المشترك."
            : result.ErrorMessage ?? "تعذر الموافقة.";

        return RedirectToAction(nameof(ClientTopUpRequests));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectClientTopUp(int id, string? adminNotes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id);
        if (account == null)
        {
            TempData["Error"] = "حساب نقطة التحصيل غير موجود.";
            return RedirectToAction(nameof(ClientTopUpRequests));
        }

        ClientWalletTopUpApprovalResult result = await _clientTopUpApprovalService.RejectAsync(
            id,
            user.Id,
            ClientWalletTopUpRecipientTarget.CollectionPoint,
            account.Id,
            adminNotes);

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "تم رفض الطلب."
            : result.ErrorMessage ?? "تعذر رفض الطلب.";

        return RedirectToAction(nameof(ClientTopUpRequests));
    }
}
