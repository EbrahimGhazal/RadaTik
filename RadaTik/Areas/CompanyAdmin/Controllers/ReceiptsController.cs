using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.ViewModels.Receipts;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
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

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        string effectiveNetworkName = (selectedNetwork.ParentNetworkId.HasValue)
            ? (await _context.Networks.AsNoTracking().Where(n => n.Id == effectiveNetworkId).Select(n => n.Name).FirstOrDefaultAsync()) ?? selectedNetwork.Name
            : selectedNetwork.Name;

        DateTime? fromDt = from?.Date;
        DateTime? toExclusive = to?.Date.AddDays(1);

        IQueryable<NetworkTopUpRequest> q = _context.NetworkTopUpRequests
            .AsNoTracking()
            .Include(r => r.PaymentMethod)
            .Include(r => r.DecidedByUser)
            .Where(r =>
                r.NetworkId == effectiveNetworkId &&
                r.Status == NetworkTopUpRequestStatus.Approved &&
                r.DecidedAt != null);

        if (fromDt.HasValue)
        {
            q = q.Where(r => r.DecidedAt!.Value >= fromDt.Value);
        }

        if (toExclusive.HasValue)
        {
            q = q.Where(r => r.DecidedAt!.Value < toExclusive.Value);
        }

        List<NetworkTopUpRequest> list = await q.OrderByDescending(r => r.DecidedAt).Take(1000).ToListAsync();

        List<ReceiptRowViewModel> rows = list.Select(r => new ReceiptRowViewModel
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

        List<ReceiptMethodSummaryViewModel> byMethod = rows
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

        ReceiptsIndexViewModel vm = new ReceiptsIndexViewModel
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

