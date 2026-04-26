using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.Receipts;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class ReceiptsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ReceiptsController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null)
    {
        ViewData["Title"] = "سجل القبض";

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

        var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var effectiveNetworkName = (selectedNetwork.ParentNetworkId.HasValue)
            ? (await _context.Networks.AsNoTracking().Where(n => n.Id == effectiveNetworkId).Select(n => n.Name).FirstOrDefaultAsync()) ?? selectedNetwork.Name
            : selectedNetwork.Name;

        DateTime? fromDt = from?.Date;
        DateTime? toExclusive = to?.Date.AddDays(1);

        var q = _context.NetworkTopUpRequests
            .AsNoTracking()
            .Include(r => r.PaymentMethod)
            .Include(r => r.DecidedByUser)
            .Where(r =>
                r.NetworkId == effectiveNetworkId &&
                r.Status == NetworkTopUpRequestStatus.Approved &&
                r.DecidedAt != null);

        if (fromDt.HasValue) q = q.Where(r => r.DecidedAt!.Value >= fromDt.Value);
        if (toExclusive.HasValue) q = q.Where(r => r.DecidedAt!.Value < toExclusive.Value);

        var list = await q.OrderByDescending(r => r.DecidedAt).Take(1000).ToListAsync();

        var rows = list.Select(r => new ReceiptRowViewModel
        {
            SourceType = ReceiptSourceType.CompanyTopUp,
            SourceId = r.Id,
            ApprovedAt = r.DecidedAt!.Value,
            ApprovedBy = r.DecidedByUser?.FullName ?? r.DecidedByUser?.UserName ?? r.DecidedByUserId,
            PartyName = effectiveNetworkName,
            PaymentMethod = r.PaymentMethod?.Name ?? r.Method ?? "—",
            ReferenceNumber = r.ReferenceNumber,
            AmountSYP = r.Amount,
            AmountUSD = 0m,
            ReceiptImagePath = r.ReceiptImagePath,
            Notes = r.Notes
        }).ToList();

        var byMethod = rows
            .GroupBy(x => string.IsNullOrWhiteSpace(x.PaymentMethod) ? "—" : x.PaymentMethod)
            .OrderByDescending(g => g.Sum(x => x.AmountSYP))
            .Select(g => new ReceiptMethodSummaryViewModel
            {
                Method = g.Key,
                Count = g.Count(),
                TotalSYP = g.Sum(x => x.AmountSYP),
                TotalUSD = g.Sum(x => x.AmountUSD)
            })
            .ToList();

        var vm = new ReceiptsIndexViewModel
        {
            Title = $"سجل القبض — {effectiveNetworkName}",
            From = fromDt,
            To = to?.Date,
            TotalCount = rows.Count,
            TotalSYP = rows.Sum(x => x.AmountSYP),
            TotalUSD = rows.Sum(x => x.AmountUSD),
            ByMethod = byMethod,
            Items = rows
        };

        return View(vm);
    }
}

