using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class SubscriberInstallationInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISubscriberInstallationInvoiceService _invoiceService;

    public SubscriberInstallationInvoicesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ISubscriberInstallationInvoiceService invoiceService)
    {
        _context = context;
        _userManager = userManager;
        _invoiceService = invoiceService;
    }

    public async Task<IActionResult> Index()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        string networkName = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == selectedNetworkId.Value)
            .Select(n => n.Name)
            .FirstOrDefaultAsync() ?? $"شركة {selectedNetworkId.Value}";

        List<SubscriberInstallationInvoiceListRowViewModel> rows = await _context.SubscriberInstallationInvoices
            .AsNoTracking()
            .Where(i => i.NetworkId == selectedNetworkId.Value)
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new SubscriberInstallationInvoiceListRowViewModel
            {
                Id = i.Id,
                ClientId = i.ClientId,
                ClientName = i.ClientName,
                Kind = i.Kind,
                Status = i.Status,
                TotalAmount = i.TotalAmount,
                PaidAmount = i.PaidAmount,
                RemainingAmount = i.RemainingAmount,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return View(new SubscriberInstallationInvoicesIndexViewModel
        {
            NetworkId = selectedNetworkId.Value,
            NetworkName = networkName,
            Rows = rows
        });
    }

    public async Task<IActionResult> Details(int id)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        SubscriberInstallationInvoice? invoice = await _context.SubscriberInstallationInvoices
            .Include(i => i.Items)
            .FirstOrDefaultAsync(i => i.Id == id && i.NetworkId == selectedNetworkId.Value);
        if (invoice == null)
        {
            return NotFound();
        }

        Client? client = await _context.Clients
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == invoice.ClientId);
        if (client == null)
        {
            return NotFound();
        }

        List<SubscriberInstallationInvoicePaymentRowViewModel> payments = (await _context.SubscriberInstallationInvoicePayments
            .AsNoTracking()
            .Where(p => p.SubscriberInstallationInvoiceId == invoice.Id)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new
            {
                p.Amount,
                p.CreatedAt,
                p.PaymentMethod,
                ReceivedByName = p.ReceivedByUser != null ? (p.ReceivedByUser.FullName ?? p.ReceivedByUser.UserName ?? "غير معروف") : "غير معروف",
                p.Notes
            })
            .ToListAsync())
            .Select(p => new SubscriberInstallationInvoicePaymentRowViewModel
            {
                Amount = p.Amount,
                PaidAt = p.CreatedAt,
                PaymentMethod = p.PaymentMethod,
                PaymentMethodLabel = SubscriberInstallationPaymentMethodLabels.Get(p.PaymentMethod),
                ReceivedByName = p.ReceivedByName,
                Notes = p.Notes
            })
            .ToList();

        SubscriberInstallationInvoiceDetailsViewModel vm = new SubscriberInstallationInvoiceDetailsViewModel
        {
            Id = invoice.Id,
            ClientId = invoice.ClientId,
            ClientName = invoice.ClientName,
            Kind = invoice.Kind,
            ReceiverMode = invoice.ReceiverMode,
            Status = invoice.Status,
            TotalAmount = invoice.TotalAmount,
            PaidAmount = invoice.PaidAmount,
            RemainingAmount = invoice.RemainingAmount,
            ClientWalletBalance = client.Balance,
            FinalizedAt = invoice.FinalizedAt,
            Items = invoice.Items.OrderBy(i => i.Id).ToList(),
            Payments = payments
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalize(int id)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue || user == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        FinalizeInvoiceResult result = await _invoiceService.FinalizeInvoiceAsync(id, selectedNetworkId.Value, user.Id);
        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "تم التثبيت النهائي وخصم المواد من المستودع. يمكنك الآن تحصيل المبلغ."
            : result.ErrorMessage;
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegisterPayment(int id, decimal amount, string? notes, SubscriberInstallationPaymentMethod paymentMethod = SubscriberInstallationPaymentMethod.Wallet)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue || user == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        RegisterInstallationPaymentResult result = await _invoiceService.RegisterPaymentAsync(
            id, selectedNetworkId.Value, user.Id, amount, paymentMethod, notes);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
        }
        else
        {
            TempData["Success"] = result.NewStatus == SubscriberInstallationInvoiceStatus.Paid
                ? "تم تسديد الفاتورة بالكامل."
                : "تم تسجيل الدفعة بنجاح.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }
}
