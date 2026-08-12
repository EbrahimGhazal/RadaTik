using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.ViewModels.CompanyAdmin;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class WalletController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<WalletController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly ICompanyWalletOnboardingFundingService _fundingService;

    public WalletController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<WalletController> logger,
        IWebHostEnvironment environment,
        ICompanyWalletOnboardingFundingService fundingService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _environment = environment;
        _fundingService = fundingService;
    }

    private async Task<(Network selected, Network effective)?> ResolveCompanyNetworksAsync(ApplicationUser user)
    {
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            return null;
        }

        Network? selectedNetwork = await _context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return null;
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effectiveNetwork = selectedNetwork.ParentNetworkId.HasValue
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        return effectiveNetwork == null ? null : (selectedNetwork, effectiveNetwork);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "محفظة الشركة";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل استخدام المحفظة.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        (Network selected, Network effective)? networks = await ResolveCompanyNetworksAsync(user);
        if (networks == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(
            _context, CashBoxOwnerType.Network, networks.Value.effective.Id);

        int pendingTopUps = await _context.NetworkTopUpRequests.AsNoTracking()
            .CountAsync(r =>
                r.NetworkId == networks.Value.effective.Id &&
                r.Status == NetworkTopUpRequestStatus.Pending);

        List<int> scopeIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, networks.Value.effective.Id);
        int pendingClientTopUps = await _context.ClientWalletTopUpRequests.AsNoTracking()
            .CountAsync(r =>
                scopeIds.Contains(r.NetworkId) &&
                r.RecipientTarget == ClientWalletTopUpRecipientTarget.CompanyManager &&
                r.Status == ClientWalletTopUpRequestStatus.Pending);

        CompanyWalletOverviewViewModel vm = new()
        {
            EffectiveCompanyNetworkId = networks.Value.effective.Id,
            EffectiveCompanyNetworkName = networks.Value.effective.Name,
            CompanyBalanceSyp = networks.Value.effective.Balance,
            CompanyBalanceUsd = networks.Value.effective.BalanceUsd,
            CashBoxBalanceSyp = cashBox?.Balance ?? 0m,
            CashBoxBalanceUsd = cashBox?.BalanceUsd ?? 0m,
            PendingTopUpRequests = pendingTopUps,
            PendingClientTopUpRequests = pendingClientTopUps
        };

        return View(vm);
    }

    /// <summary>رابط قديم — إعادة توجيه إلى طلب التعبئة (لا تغذية مباشرة من الصندوق).</summary>
    [HttpGet]
    public IActionResult ObsoleteFundFromCashBoxRedirect()
    {
        TempData["Info"] =
            "لم تعد هذه الصفحة متاحة. استخدم «طلب تعبية رصيد»؛ ويمكنك اختيار خصم المبلغ من الصندوق عند موافقة مدير النظام فقط.";
        return RedirectToRoutePermanent("networkManager-wallet-topup");
    }

    [HttpGet]
    public async Task<IActionResult> TopUp()
    {
        ViewData["Title"] = "تغذية رصيد";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل استخدام المحفظة.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(
            _context, CashBoxOwnerType.Network, effectiveNetworkId);

        CompanyWalletOnboardingFundingStatus funding =
            await _fundingService.EvaluateAsync(effectiveNetworkId, HttpContext.RequestAborted);

        CompanyWalletTopUpViewModel vm = new CompanyWalletTopUpViewModel
        {
            SelectedNetworkId = selectedNetwork.Id,
            SelectedNetworkName = selectedNetwork.Name,
            EffectiveCompanyNetworkId = effectiveNetworkId,
            EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name,
            CompanyBalance = effectiveNetwork?.Balance ?? 0m,
            CompanyBalanceUsd = effectiveNetwork?.BalanceUsd ?? 0m,
            CashBoxBalanceSyp = cashBox?.Balance ?? 0m,
            CashBoxBalanceUsd = cashBox?.BalanceUsd ?? 0m,
            Amount = funding.RequiresFundingGate ? funding.MinimumRequiredSyp : 0m,
            MinimumRequiredSyp = funding.RequiresFundingGate ? funding.MinimumRequiredSyp : 0m,
            IsOnboardingFundingRequired = funding.RequiresFundingGate
        };

        if (funding.RequiresFundingGate)
        {
            TempData["WalletOnboardingStep"] = "1";
        }

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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        model.SelectedNetworkId = selectedNetwork.Id;
        model.SelectedNetworkName = selectedNetwork.Name;
        model.EffectiveCompanyNetworkId = effectiveNetworkId;
        model.EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name;
        model.CompanyBalance = effectiveNetwork?.Balance ?? 0m;
        model.CompanyBalanceUsd = effectiveNetwork?.BalanceUsd ?? 0m;
        CashBox? cashBoxReload = await CashBoxHelper.GetOrCreateCashBoxAsync(
            _context, CashBoxOwnerType.Network, effectiveNetworkId);
        model.CashBoxBalanceSyp = cashBoxReload?.Balance ?? 0m;
        model.CashBoxBalanceUsd = cashBoxReload?.BalanceUsd ?? 0m;

        CompanyWalletOnboardingFundingStatus funding =
            await _fundingService.EvaluateAsync(effectiveNetworkId, HttpContext.RequestAborted);
        model.MinimumRequiredSyp = funding.RequiresFundingGate ? funding.MinimumRequiredSyp : 0m;
        model.IsOnboardingFundingRequired = funding.RequiresFundingGate;

        List<PaymentMethod> paymentMethods = await _context.PaymentMethods
            .AsNoTracking()
            .Where(m => m.IsActive)
            .OrderBy(m => m.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
        ViewBag.PaymentMethods = paymentMethods;

        if (funding.RequiresFundingGate && model.Amount < funding.MinimumRequiredSyp)
        {
            ModelState.AddModelError(
                nameof(model.Amount),
                $"يجب ألا يقل مبلغ التعبئة عن سعر إنشاء الشركة: {SyrianCurrencyHelper.FormatNew(funding.MinimumRequiredSyp)} ل.س.ج.");
        }

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
                string[] allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                string? ext = Path.GetExtension(model.ReceiptImage.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.ReceiptImage), "يرجى رفع صورة بصيغة مقبولة (JPG, PNG, GIF, WebP).");
                    return View(model);
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
                    await model.ReceiptImage.CopyToAsync(stream);
                }

                receiptPath = $"/uploads/receipts/{uniqueFileName}";
            }

            NetworkTopUpRequest req = new NetworkTopUpRequest
            {
                NetworkId = effectiveNetworkId,
                Amount = model.Amount,
                PaymentMethodId = pm?.Id,
                Method = pm?.Name ?? (string.IsNullOrWhiteSpace(model.Method) ? null : model.Method.Trim()),
                ReferenceNumber = model.ReferenceNumber.Trim(),
                ReceiptImagePath = receiptPath,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                DeductFromCompanyCashBoxOnApproval = false,
                Status = NetworkTopUpRequestStatus.Pending,
                RequestedByUserId = user.Id,
                RequestedAt = DateTime.Now
            };

            _context.NetworkTopUpRequests.Add(req);
            await _context.SaveChangesAsync();

            if (funding.RequiresFundingGate && model.Amount >= funding.MinimumRequiredSyp)
            {
                TempData["Success"] =
                    "تم إرسال طلب التعبئة. يمكنك متابعة استخدام النظام بينما يراجع مدير النظام الطلب.";
                return RedirectToRoute("networkManager-dashboard");
            }

            TempData["Success"] = AppMessages.OperationSuccess;
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل استخدام المحفظة.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        List<NetworkWalletTransaction> txs = await _context.NetworkWalletTransactions
            .AsNoTracking()
            .Include(t => t.CreatedByUser)
            .Where(t => t.NetworkId == effectiveNetworkId)
            .OrderByDescending(t => t.CreatedAt)
            .Take(500)
            .ToListAsync();

        CompanyWalletTransactionsViewModel vm = new CompanyWalletTransactionsViewModel
        {
            EffectiveCompanyNetworkId = effectiveNetworkId,
            EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name,
            CompanyBalance = effectiveNetwork?.Balance ?? 0m,
            CompanyBalanceUsd = effectiveNetwork?.BalanceUsd ?? 0m,
            Transactions = txs
        };

        return View(vm);
    }

}

