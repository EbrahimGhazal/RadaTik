using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Warehouse)]
public class MaterialPurchaseInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWarehouseMaterialInvoiceService _invoiceService;
    private readonly IWarehouseStockService _warehouseStock;

    public MaterialPurchaseInvoicesController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IWarehouseMaterialInvoiceService invoiceService,
      IWarehouseStockService warehouseStock)
    {
        _context = context;
        _userManager = userManager;
        _invoiceService = invoiceService;
        _warehouseStock = warehouseStock;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewData["Title"] = "فواتير شراء المواد";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Invoices = await _context.MaterialPurchaseInvoices
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == scope.CompanyNetworkId)
          .OrderByDescending(i => i.InvoiceDate)
          .ThenByDescending(i => i.Id)
          .Take(100)
          .ToListAsync();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        ViewData["Title"] = "فاتورة شراء مواد";
        ViewBag.BusinessModuleHint = "مادة جديدة: اختر «— مادة جديدة —» ثم اكتب الاسم. قطع المخزون = عدد الوحدات × قطع داخل الوحدة (مثال: 5 كرتون × 16 = 80 قطعة).";
        ViewBag.WarehouseHintsMode = "purchase";
        await PopulatePurchaseCreateViewAsync(scope);
        return View(new MaterialPurchaseInvoiceFormViewModel
        {
            Currency = null,
            PaymentStatus = null,
            WarehouseItems = await LoadWarehouseRowsAsync(scope.CompanyNetworkId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
      DateTime invoiceDate,
      string? supplierName,
      int? erpSupplierId,
      string? paymentStatus,
      string? notes,
      PricingCurrency? currency,
      string? cashShortfallAction,
      List<MaterialInvoiceLineInput>? lines)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        List<MaterialInvoiceLineInput> postedLines = lines ?? [];
        MaterialPurchaseInvoiceFormViewModel formModel = new()
        {
            InvoiceDate = invoiceDate,
            SupplierName = supplierName,
            ErpSupplierId = erpSupplierId,
            PaymentStatus = paymentStatus,
            Currency = currency,
            Notes = notes,
            Lines = postedLines,
            WarehouseItems = await LoadWarehouseRowsAsync(scope.CompanyNetworkId)
        };

        if (string.Equals(cashShortfallAction, "abort", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "تم إلغاء حفظ فاتورة الشراء بناءً على طلبك.";
            return RedirectToAction(nameof(Index));
        }

        if (string.Equals(cashShortfallAction, "saveUnpaid", StringComparison.OrdinalIgnoreCase))
        {
            paymentStatus = "unpaid";
            formModel.PaymentStatus = "unpaid";
        }

        if (!currency.HasValue || (currency != PricingCurrency.SYP_New && currency != PricingCurrency.USD))
        {
            TempData["Error"] = "اختر عملة الفاتورة (ل.س.ج أو $).";
            await PopulatePurchaseCreateViewAsync(scope);
            return View(formModel);
        }

        if (paymentStatus is not "paid" and not "unpaid")
        {
            TempData["Error"] = "اختر حالة الدفع: مدفوعة أو غير مدفوعة.";
            await PopulatePurchaseCreateViewAsync(scope);
            return View(formModel);
        }

        bool isPaid = paymentStatus == "paid";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        MaterialInvoiceResult result = await _invoiceService.CreatePurchaseInvoiceAsync(
          scope.CompanyNetworkId,
          user?.Id,
          invoiceDate,
          supplierName,
          isPaid,
          false,
          notes,
          postedLines,
          currency.Value,
          erpSupplierId,
          HttpContext.RequestAborted);

        if (result.RequiresUnpaidOrCancelChoice)
        {
            ViewBag.ShowCashShortfallConfirm = true;
            ViewBag.CashShortfallMessage = result.ErrorMessage;
            ViewBag.CashShortfallRequired = result.RequiredAmount;
            ViewBag.CashShortfallAvailable = result.AvailableCash;
            ViewBag.CashShortfallCurrency = result.Currency ?? currency.Value;
            await PopulatePurchaseCreateViewAsync(scope);
            return View(formModel);
        }

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            await PopulatePurchaseCreateViewAsync(scope);
            return View(formModel);
        }

        TempData["Success"] = isPaid
          ? "تم حفظ فاتورة الشراء وتحديث المستودع وخصم المبلغ من الصندوق."
          : "تم حفظ فاتورة الشراء كغير مدفوعة وتحديث المستودع.";
        return RedirectToAction(nameof(Details), new { id = result.InvoiceId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        MaterialPurchaseInvoice? invoice = await LoadPurchaseInvoiceAsync(scope.CompanyNetworkId, id);
        if (invoice == null)
        {
            TempData["Error"] = "الفاتورة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"فاتورة شراء #{invoice.Id}";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(invoice);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        MaterialPurchaseInvoice? invoice = await LoadPurchaseInvoiceAsync(scope.CompanyNetworkId, id);
        if (invoice == null)
        {
            TempData["Error"] = "الفاتورة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        if (invoice.IsCancelled)
        {
            TempData["Error"] = "لا يمكن تعديل فاتورة ملغاة.";
            return RedirectToAction(nameof(Details), new { id });
        }

        ViewData["Title"] = $"تعديل فاتورة شراء #{invoice.Id}";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.ErpSuppliers = await ErpLookupHelper.GetActiveSuppliersAsync(_context, scope.CompanyNetworkId);
        return View(new MaterialPurchaseInvoiceEditViewModel
        {
            Id = invoice.Id,
            InvoiceDate = invoice.InvoiceDate,
            SupplierName = invoice.SupplierName,
            ErpSupplierId = invoice.ErpSupplierId,
            IsPaid = invoice.IsPaid,
            Notes = invoice.Notes,
            TotalAmount = invoice.TotalAmount
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(
      int id,
      DateTime invoiceDate,
      string? supplierName,
      int? erpSupplierId,
      string? paymentStatus,
      string? notes)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يجب تسجيل الدخول.";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (paymentStatus is not "paid" and not "unpaid")
        {
            TempData["Error"] = "اختر حالة الدفع: مدفوعة أو غير مدفوعة.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        bool isPaid = paymentStatus == "paid";

        MaterialInvoiceResult result = await _invoiceService.UpdatePurchaseInvoiceAsync(
          scope.CompanyNetworkId,
          id,
          user.Id,
          invoiceDate,
          supplierName,
          isPaid,
          false,
          notes,
          erpSupplierId,
          HttpContext.RequestAborted);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Edit), new { id });
        }

        TempData["Success"] = "تم تحديث بيانات الفاتورة.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, bool refundWallet)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يجب تسجيل الدخول.";
            return RedirectToAction(nameof(Details), new { id });
        }

        MaterialInvoiceResult result = await _invoiceService.CancelPurchaseInvoiceAsync(
          scope.CompanyNetworkId, id, user.Id, refundWallet, HttpContext.RequestAborted);

        TempData[result.Success ? "Success" : "Error"] = result.Success
          ? "تم إلغاء الفاتورة وعكس حركات المستودع."
          : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Print(int id)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        MaterialPurchaseInvoice? invoice = await LoadPurchaseInvoiceAsync(scope.CompanyNetworkId, id);
        if (invoice == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.PrintDate = DateTime.Today.ToString("yyyy-MM-dd");
        return View(invoice);
    }

    private async Task PopulatePurchaseCreateViewAsync(CompanyBusinessScopeHelper.CompanyScope scope)
    {
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.ErpSuppliers = await ErpLookupHelper.GetActiveSuppliersAsync(_context, scope.CompanyNetworkId);
        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(
          _context, CashBoxOwnerType.Network, scope.CompanyNetworkId);
        ViewBag.CashBoxBalanceSyp = cashBox?.Balance ?? 0m;
        ViewBag.CashBoxBalanceUsd = cashBox?.BalanceUsd ?? 0m;
    }

    private async Task<MaterialPurchaseInvoice?> LoadPurchaseInvoiceAsync(int companyNetworkId, int id) =>
      await _context.MaterialPurchaseInvoices
        .AsNoTracking()
        .Include(i => i.ErpSupplier)
        .Include(i => i.Lines)
        .ThenInclude(l => l.WarehouseItem)
        .Include(i => i.CreatedByUser)
        .FirstOrDefaultAsync(i => i.Id == id && i.CompanyNetworkId == companyNetworkId);

    private async Task<List<WarehouseItemRowViewModel>> LoadWarehouseRowsAsync(int companyNetworkId)
    {
        Dictionary<int, decimal> onHand = await _warehouseStock.GetOnHandByItemIdAsync(companyNetworkId);
        List<WarehouseItem> items = await _context.WarehouseItems
          .AsNoTracking()
          .Where(i => i.CompanyNetworkId == companyNetworkId && i.IsActive)
          .OrderBy(i => i.Name)
          .ToListAsync();

        return items.Select(i => new WarehouseItemRowViewModel
        {
            Id = i.Id,
            Name = i.Name,
            ModelNumber = i.ModelNumber,
            Sku = i.Sku,
            Unit = i.Unit,
            PurchasePrice = i.PurchasePrice,
            PurchaseCurrency = i.PurchaseCurrency,
            WholesalePrice = i.WholesalePrice,
            RetailPrice = i.RetailPrice,
            OnHand = onHand.GetValueOrDefault(i.Id, 0m),
            IsActive = i.IsActive
        }).ToList();
    }
}
