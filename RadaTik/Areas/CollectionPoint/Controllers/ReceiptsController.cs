using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.ViewModels.Receipts;

namespace RadaTik.Areas.CollectionPoint.Controllers;

[Area("CollectionPoint")]
[Authorize(Roles = $"{RoleNames.CollectionPoint},{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
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
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var account = await _context.CollectionPointAccounts
            .AsNoTracking()
            .Include(a => a.User)
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        if (account == null)
        {
            TempData["Error"] = "لم يتم العثور على حساب نقطة التحصيل.";
            return RedirectToAction("Index", "Home", new { area = "CollectionPoint" });
        }

        DateTime? fromDt = from?.Date;
        DateTime? toExclusive = to?.Date.AddDays(1);

        var q = _context.CollectionPointTopUpRequests
            .AsNoTracking()
            .Include(r => r.PaymentMethod)
            .Include(r => r.TargetNetwork)
            .Include(r => r.ProcessedByUser)
            .Where(r =>
                r.CollectionPointAccountId == account.Id &&
                r.Status == CollectionPointTopUpStatus.Approved &&
                r.ProcessedAt != null);

        if (fromDt.HasValue) q = q.Where(r => r.ProcessedAt!.Value >= fromDt.Value);
        if (toExclusive.HasValue) q = q.Where(r => r.ProcessedAt!.Value < toExclusive.Value);

        var list = await q.OrderByDescending(r => r.ProcessedAt).Take(1000).ToListAsync();

        var rows = list.Select(r => new ReceiptRowViewModel
        {
            SourceType = ReceiptSourceType.CollectionPointTopUp,
            SourceId = r.Id,
            ApprovedAt = r.ProcessedAt!.Value,
            ApprovedBy = r.ProcessedByUser?.FullName ?? r.ProcessedByUser?.UserName ?? r.ProcessedByUserId,
            PartyName = account.User?.UserName ?? account.UserId,
            PaymentMethod = r.PaymentMethod?.Name ?? r.Method ?? "—",
            ReferenceNumber = r.ReferenceNumber,
            AmountSYP = r.Amount,
            AmountUSD = 0m,
            ReceiptImagePath = r.ReceiptImagePath,
            Notes = r.Notes
        }).ToList();

        var txRows = await _context.PaymentTransactions
            .AsNoTracking()
            .Include(t => t.Client)
            .Include(t => t.ReceivedByUser)
            .Where(t => t.ReceivedByUserId == user.Id)
            .OrderByDescending(t => t.PaymentDate)
            .Take(500)
            .Select(t => new ReceiptRowViewModel
            {
                SourceType = ReceiptSourceType.CollectionPointOperation,
                SourceId = t.Id,
                ApprovedAt = t.PaymentDate,
                ApprovedBy = t.ReceivedByUser != null ? (t.ReceivedByUser.FullName ?? t.ReceivedByUser.UserName) : t.ReceivedByUserId,
                PartyName = t.Client != null ? (t.Client.Name ?? t.Client.UserName) : t.ClientId.ToString(),
                PaymentMethod = t.OperationType ?? "ReceivePayment",
                ReferenceNumber = t.ReferenceNumber,
                AmountSYP = t.Amount,
                AmountUSD = 0m,
                ReceiptImagePath = null,
                Notes = t.Notes
            })
            .ToListAsync();

        rows.AddRange(txRows);
        rows = rows.OrderByDescending(x => x.ApprovedAt).ToList();

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
            Title = "سجل القبض — نقطة التحصيل",
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

