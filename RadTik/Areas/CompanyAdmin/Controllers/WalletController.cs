using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.CompanyAdmin;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class WalletController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WalletController> _logger;
    private readonly IWebHostEnvironment _environment;

    public WalletController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<WalletController> logger, IWebHostEnvironment environment)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> TopUp()
    {
        ViewData["Title"] = "تغذية رصيد";

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل استخدام المحفظة.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        var vm = new CompanyWalletTopUpViewModel
        {
            SelectedNetworkId = selectedNetwork.Id,
            SelectedNetworkName = selectedNetwork.Name,
            EffectiveCompanyNetworkId = effectiveNetworkId,
            EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name,
            CompanyBalance = effectiveNetwork?.Balance ?? 0m,
            Amount = 0
        };

        ViewBag.PaymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TopUp(CompanyWalletTopUpViewModel model)
    {
        ViewData["Title"] = "تغذية رصيد";

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        model.SelectedNetworkId = selectedNetwork.Id;
        model.SelectedNetworkName = selectedNetwork.Name;
        model.EffectiveCompanyNetworkId = effectiveNetworkId;
        model.EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name;
        model.CompanyBalance = effectiveNetwork?.Balance ?? 0m;

        var paymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
        ViewBag.PaymentMethods = paymentMethods;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            PaymentMethod? pm = null;
            if (paymentMethods.Count > 0)
            {
                if (!model.PaymentMethodId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.PaymentMethodId), "يرجى اختيار طريقة الدفع.");
                    return View(model);
                }

                pm = paymentMethods.FirstOrDefault(x => x.Id == model.PaymentMethodId.Value);
                if (pm == null)
                {
                    ModelState.AddModelError(nameof(model.PaymentMethodId), "طريقة الدفع غير صالحة.");
                    return View(model);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(model.Method))
                {
                    ModelState.AddModelError(nameof(model.Method), "يرجى إدخال طريقة الدفع.");
                    return View(model);
                }
            }

            if (string.IsNullOrWhiteSpace(model.ReferenceNumber))
            {
                ModelState.AddModelError(nameof(model.ReferenceNumber), "يرجى إدخال رقم الإشعار/المرجع.");
                return View(model);
            }

            if (model.ReceiptImage == null || model.ReceiptImage.Length == 0)
            {
                ModelState.AddModelError(nameof(model.ReceiptImage), "يرجى رفع صورة الإيصال.");
                return View(model);
            }
            if (ImageUploadRules.IsTooLarge(model.ReceiptImage))
            {
                ModelState.AddModelError(nameof(model.ReceiptImage), ImageUploadRules.MaxReceiptImageSizeMessage);
                return View(model);
            }

            string? receiptPath = null;
            if (model.ReceiptImage != null && model.ReceiptImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(model.ReceiptImage.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.ReceiptImage), "يرجى رفع صورة بصيغة مقبولة (JPG, PNG, GIF, WebP).");
                    return View(model);
                }
                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (var stream = new FileStream(filePath, FileMode.Create))
                    await model.ReceiptImage.CopyToAsync(stream);
                receiptPath = $"/uploads/receipts/{uniqueFileName}";
            }

            var req = new NetworkTopUpRequest
            {
                NetworkId = effectiveNetworkId,
                Amount = model.Amount,
                PaymentMethodId = pm?.Id,
                Method = pm?.Name ?? (string.IsNullOrWhiteSpace(model.Method) ? null : model.Method.Trim()),
                ReferenceNumber = model.ReferenceNumber.Trim(),
                ReceiptImagePath = receiptPath,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                Status = NetworkTopUpRequestStatus.Pending,
                RequestedByUserId = user.Id,
                RequestedAt = DateTime.Now
            };

            _context.NetworkTopUpRequests.Add(req);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إرسال طلب تغذية الرصيد لمدير النظام للموافقة.";
            return RedirectToAction(nameof(TopUp));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create top-up request.");
            TempData["Error"] = "تعذر إرسال الطلب. حاول مرة أخرى.";
            return View(model);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Transactions()
    {
        ViewData["Title"] = "عمليات التعبئة";

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل استخدام المحفظة.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        var txs = await _context.NetworkWalletTransactions
            .AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Where(t => t.NetworkId == effectiveNetworkId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(500)
            .ToListAsync();

        var vm = new CompanyWalletTransactionsViewModel
        {
            EffectiveCompanyNetworkId = effectiveNetworkId,
            EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name,
            CompanyBalance = effectiveNetwork?.Balance ?? 0m,
            Transactions = txs
        };

        return View(vm);
    }

}

