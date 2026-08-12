using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class ClientWalletTopUpRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ClientWalletTopUpApprovalService _approvalService;

    public ClientWalletTopUpRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ClientWalletTopUpApprovalService approvalService)
    {
        _context = context;
        _userManager = userManager;
        _approvalService = approvalService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ClientWalletTopUpRequestStatus? status = null)
    {
        ViewData["Title"] = "طلبات تغذية المشتركين";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        Network? network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
        int companyNetworkId = network?.ParentNetworkId ?? networkId.Value;
        List<int> scopeIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        IQueryable<ClientWalletTopUpRequest> query = _context.ClientWalletTopUpRequests
            .AsNoTracking()
            .Include(r => r.Client)
            .Include(r => r.PaymentMethod)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ProcessedByUser)
            .Where(r =>
                scopeIds.Contains(r.NetworkId) &&
                r.RecipientTarget == ClientWalletTopUpRecipientTarget.CompanyManager);

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        List<ClientWalletTopUpRequest> items = await query
            .OrderByDescending(r => r.RequestedAt)
            .Take(500)
            .ToListAsync();

        ViewBag.Items = items;
        ViewBag.SelectedStatus = status;
        ViewBag.PendingCount = await _context.ClientWalletTopUpRequests.CountAsync(r =>
            scopeIds.Contains(r.NetworkId) &&
            r.RecipientTarget == ClientWalletTopUpRecipientTarget.CompanyManager &&
            r.Status == ClientWalletTopUpRequestStatus.Pending);
        ViewBag.CompanyName = network?.Name ?? "";

        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? adminNotes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        ClientWalletTopUpApprovalResult result = await _approvalService.ApproveAsync(
            id,
            user.Id,
            ClientWalletTopUpRecipientTarget.CompanyManager,
            null,
            adminNotes);

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "تمت الموافقة على طلب التغذية وإضافة الرصيد لمحفظة المشترك."
            : result.ErrorMessage ?? "تعذر الموافقة على الطلب.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? adminNotes = null)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        ClientWalletTopUpApprovalResult result = await _approvalService.RejectAsync(
            id,
            user.Id,
            ClientWalletTopUpRecipientTarget.CompanyManager,
            null,
            adminNotes);

        TempData[result.Success ? "Success" : "Error"] = result.Success
            ? "تم رفض طلب التغذية."
            : result.ErrorMessage ?? "تعذر رفض الطلب.";

        return RedirectToAction(nameof(Index));
    }
}
