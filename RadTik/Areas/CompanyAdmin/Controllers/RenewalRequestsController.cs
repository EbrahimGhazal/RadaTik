using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class RenewalRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMikroTikUsersService _mikroTikService;
    private readonly IClientRenewalGuardService _clientRenewalGuardService;
    private readonly ILogger<RenewalRequestsController> _logger;

    public RenewalRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikUsersService mikroTikService,
        IClientRenewalGuardService clientRenewalGuardService,
        ILogger<RenewalRequestsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _mikroTikService = mikroTikService;
        _clientRenewalGuardService = clientRenewalGuardService;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CollectionPointRenewalStatus? status = null)
    {
        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction("Index", "Network");
        }

        var query = _context.CollectionPointRenewalRequests
            .Include(r => r.Client)
            .Include(r => r.RequestedByUser)
            .Where(r => r.NetworkId == networkId.Value);

        if (status.HasValue)
            query = query.Where(r => r.Status == status.Value);

        var requests = await query.OrderByDescending(r => r.RequestedAt).ToListAsync();

        ViewBag.Status = status;
        ViewBag.PendingCount = await _context.CollectionPointRenewalRequests.CountAsync(r => r.NetworkId == networkId.Value && r.Status == CollectionPointRenewalStatus.Pending);
        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, int? renewDays, DateTime? newExpirationDate)
    {
        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue) return RedirectToAction("Index", "Network");

        var request = await _context.CollectionPointRenewalRequests
            .Include(r => r.Client)
            .FirstOrDefaultAsync(r => r.Id == id && r.NetworkId == networkId.Value && r.Status == CollectionPointRenewalStatus.Pending);
        if (request == null) { TempData["Error"] = "الطلب غير موجود أو تمت معالجته."; return RedirectToAction(nameof(Index)); }

        DateTime newExp;
        if (renewDays.HasValue && renewDays.Value > 0)
            newExp = DateTime.Now.AddDays(renewDays.Value);
        else if (newExpirationDate.HasValue)
            newExp = newExpirationDate.Value;
        else
            newExp = (request.Client?.AccountExpirationDate ?? DateTime.Now).AddMonths(1);

        try
        {
            var client = request.Client!;
            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                TempData["Error"] =
                    $"لا يمكن قبول طلب التجديد قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
                return RedirectToAction(nameof(Index));
            }

            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                await _mikroTikService.RenewPPPoESubscription(client.UserName, client.MikroTikServerId.Value, newExp);
            }
            client.AccountExpirationDate = newExp;
            client.LastUpdated = DateTime.Now;

            request.Status = CollectionPointRenewalStatus.Approved;
            request.ProcessedByUserId = user!.Id;
            request.ProcessedAt = DateTime.Now;
            request.NewExpirationDate = newExp;

            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم قبول طلب التجديد وتمديد اشتراك العميل حتى {newExp:yyyy/MM/dd}.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ في قبول طلب التجديد {RequestId}", id);
            TempData["Error"] = "حدث خطأ أثناء تنفيذ التجديد: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? adminNotes)
    {
        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue) return RedirectToAction("Index", "Network");

        var request = await _context.CollectionPointRenewalRequests
            .FirstOrDefaultAsync(r => r.Id == id && r.NetworkId == networkId.Value && r.Status == CollectionPointRenewalStatus.Pending);
        if (request == null) { TempData["Error"] = "الطلب غير موجود أو تمت معالجته."; return RedirectToAction(nameof(Index)); }

        request.Status = CollectionPointRenewalStatus.Rejected;
        request.ProcessedByUserId = user!.Id;
        request.ProcessedAt = DateTime.Now;
        request.AdminNotes = adminNotes;
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم رفض طلب التجديد.";
        return RedirectToAction(nameof(Index));
    }
}
