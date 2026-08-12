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
public class MaterialSalesInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWarehouseMaterialInvoiceService _invoiceService;
    private readonly IWarehouseStockService _warehouseStock;

    public MaterialSalesInvoicesController(
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

        ViewData["Title"] = "فواتير بيع المواد";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Invoices = await _context.MaterialSalesInvoices
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

        ViewData["Title"] = "فاتورة بيع مواد";
        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.BusinessModuleHint = "عند «تم الدفع» يُودَع المبلغ في الصندوق النقدي بنفس عملة الفاتورة.";
        ViewBag.ErpCustomers = await ErpLookupHelper.GetActiveCustomersAsync(_context, scope.CompanyNetworkId);
        return View(new MaterialSalesInvoiceFormViewModel
        {
            Currency = null,
            PaymentStatus = null,
            PriceMode = null,
            WarehouseItems = await LoadWarehouseRowsAsync(scope.CompanyNetworkId)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
      DateTime invoiceDate,
      string? customerName,
      int? erpCustomerId,
      string? paymentStatus,
      MaterialSalePriceMode? priceMode,
      string? notes,
      PricingCurrency? currency,
      List<MaterialSalesLineInput>? lines)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        MaterialSalesInvoiceFormViewModel formModel = new()
        {
            InvoiceDate = invoiceDate,
            CustomerName = customerName,
            ErpCustomerId = erpCustomerId,
            PaymentStatus = paymentStatus,
            PriceMode = priceMode,
            Currency = currency,
            Notes = notes,
            WarehouseItems = await LoadWarehouseRowsAsync(scope.CompanyNetworkId)
        };

        if (!currency.HasValue || (currency != PricingCurrency.SYP_New && currency != PricingCurrency.USD))
        {
            TempData["Error"] = "اختر عملة الفاتورة (ل.س.ج أو $).";
            ViewBag.ErpCustomers = await ErpLookupHelper.GetActiveCustomersAsync(_context, scope.CompanyNetworkId);
            return View(formModel);
        }

        if (paymentStatus is not "paid" and not "unpaid")
        {
            TempData["Error"] = "اختر حالة الدفع: مدفوعة أو غير مدفوعة.";
            ViewBag.ErpCustomers = await ErpLookupHelper.GetActiveCustomersAsync(_context, scope.CompanyNetworkId);
            return View(formModel);
        }

        if (priceMode is not MaterialSalePriceMode.Wholesale
            and not MaterialSalePriceMode.Retail
            and not MaterialSalePriceMode.Custom)
        {
            TempData["Error"] = "اختر نوع السعر: جملة أو مفرق أو مخصص.";
            ViewBag.ErpCustomers = await ErpLookupHelper.GetActiveCustomersAsync(_context, scope.CompanyNetworkId);
            return View(formModel);
        }

        bool isPaid = paymentStatus == "paid";
        List<MaterialSalesLineInput> normalizedLines = (lines ?? []).Select(l => new MaterialSalesLineInput
        {
            WarehouseItemId = l.WarehouseItemId,
            PriceMode = priceMode!.Value,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice > 0m ? l.UnitPrice : (l.CustomUnitPrice ?? 0m),
            CustomUnitPrice = l.CustomUnitPrice
        }).ToList();

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        MaterialInvoiceResult result = await _invoiceService.CreateSalesInvoiceAsync(
          scope.CompanyNetworkId,
          user?.Id,
          invoiceDate,
          customerName,
          isPaid,
          false,
          notes,
          normalizedLines,
          currency.Value,
          erpCustomerId,
          HttpContext.RequestAborted);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            ViewBag.ErpCustomers = await ErpLookupHelper.GetActiveCustomersAsync(_context, scope.CompanyNetworkId);
            return View(formModel);
        }

        TempData["Success"] = "تم حفظ فاتورة البيع وخصم المستودع.";
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

        MaterialSalesInvoice? invoice = await LoadSalesInvoiceAsync(scope.CompanyNetworkId, id);
        if (invoice == null)
        {
            TempData["Error"] = "الفاتورة غير موجودة.";
            return RedirectToAction(nameof(Index));
        }

        ViewData["Title"] = $"فاتورة بيع #{invoice.Id}";
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

        MaterialSalesInvoice? invoice = await LoadSalesInvoiceAsync(scope.CompanyNetworkId, id);
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

        ViewData["Title"] = $"تعديل فاتورة بيع #{invoice.Id}";
        ViewBag.ErpCustomers = await ErpLookupHelper.GetActiveCustomersAsync(_context, scope.CompanyNetworkId);
        return View(new MaterialSalesInvoiceEditViewModel
        {
            Id = invoice.Id,
            InvoiceDate = invoice.InvoiceDate,
            CustomerName = invoice.CustomerName,
            ErpCustomerId = invoice.ErpCustomerId,
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
      string? customerName,
      int? erpCustomerId,
      bool isPaid,
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

        MaterialInvoiceResult result = await _invoiceService.UpdateSalesInvoiceAsync(
          scope.CompanyNetworkId,
          id,
          user.Id,
          invoiceDate,
          customerName,
          isPaid,
          false,
          notes,
          erpCustomerId,
          HttpContext.RequestAborted);

        TempData[result.Success ? "Success" : "Error"] = result.Success
          ? "تم تحديث بيانات الفاتورة."
          : result.ErrorMessage;
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

        MaterialInvoiceResult result = await _invoiceService.CancelSalesInvoiceAsync(
          scope.CompanyNetworkId, id, user.Id, refundWallet, HttpContext.RequestAborted);

        TempData[result.Success ? "Success" : "Error"] = result.Success
          ? "تم إلغاء الفاتورة وإرجاع الكميات للمستودع."
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

        MaterialSalesInvoice? invoice = await LoadSalesInvoiceAsync(scope.CompanyNetworkId, id);
        if (invoice == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.PrintDate = DateTime.Today.ToString("yyyy-MM-dd");
        return View(invoice);
    }

    private async Task<MaterialSalesInvoice?> LoadSalesInvoiceAsync(int companyNetworkId, int id) =>
      await _context.MaterialSalesInvoices
        .AsNoTracking()
        .Include(i => i.ErpCustomer)
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
