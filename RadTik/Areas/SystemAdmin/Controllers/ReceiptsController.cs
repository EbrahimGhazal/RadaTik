using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.Receipts;

namespace RadTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class ReceiptsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReceiptsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null)
    {
        ViewData["Title"] = "سجل القبض المركزي";

        DateTime? fromDt = from?.Date;
        DateTime? toExclusive = to?.Date.AddDays(1);

        var companyQ = _context.NetworkTopUpRequests
            .AsNoTracking()
            .Include(r => r.Network)
            .Include(r => r.PaymentMethod)
            .Include(r => r.RequestedByUser)
            .Include(r => r.DecidedByUser)
            .Where(r => r.Status == NetworkTopUpRequestStatus.Approved && r.DecidedAt != null);

        if (fromDt.HasValue) companyQ = companyQ.Where(r => r.DecidedAt!.Value >= fromDt.Value);
        if (toExclusive.HasValue) companyQ = companyQ.Where(r => r.DecidedAt!.Value < toExclusive.Value);

        var cpQ = _context.CollectionPointTopUpRequests
            .AsNoTracking()
            .Include(r => r.PaymentMethod)
            .Include(r => r.CollectionPointAccount)
                .ThenInclude(a => a!.User)
            .Include(r => r.CollectionPointAccount)
                .ThenInclude(a => a!.Network)
            .Include(r => r.RequestedByUser)
            .Include(r => r.ProcessedByUser)
            .Where(r =>
                r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin &&
                r.Status == CollectionPointTopUpStatus.Approved &&
                r.ProcessedAt != null);

        if (fromDt.HasValue) cpQ = cpQ.Where(r => r.ProcessedAt!.Value >= fromDt.Value);
        if (toExclusive.HasValue) cpQ = cpQ.Where(r => r.ProcessedAt!.Value < toExclusive.Value);

        var company = await companyQ.OrderByDescending(r => r.DecidedAt).Take(1000).ToListAsync();
        var cps = await cpQ.OrderByDescending(r => r.ProcessedAt).Take(1000).ToListAsync();

        var rows = new List<ReceiptRowViewModel>(company.Count + cps.Count);

        foreach (var r in company)
        {
            rows.Add(new ReceiptRowViewModel
            {
                SourceType = ReceiptSourceType.CompanyTopUp,
                SourceId = r.Id,
                ApprovedAt = r.DecidedAt!.Value,
                ApprovedBy = r.DecidedByUser?.FullName ?? r.DecidedByUser?.UserName ?? r.DecidedByUserId,
                PartyName = r.Network?.Name,
                PaymentMethod = r.PaymentMethod?.Name ?? r.Method ?? "—",
                ReferenceNumber = r.ReferenceNumber,
                AmountSYP = r.Amount,
                AmountUSD = 0m,
                ReceiptImagePath = r.ReceiptImagePath,
                Notes = r.Notes
            });
        }

        foreach (var r in cps)
        {
            rows.Add(new ReceiptRowViewModel
            {
                SourceType = ReceiptSourceType.CollectionPointTopUp,
                SourceId = r.Id,
                ApprovedAt = r.ProcessedAt!.Value,
                ApprovedBy = r.ProcessedByUser?.FullName ?? r.ProcessedByUser?.UserName ?? r.ProcessedByUserId,
                PartyName = r.CollectionPointAccount?.User?.UserName ?? r.RequestedByUser?.UserName,
                PaymentMethod = r.PaymentMethod?.Name ?? r.Method ?? "—",
                ReferenceNumber = r.ReferenceNumber,
                AmountSYP = r.Amount,
                AmountUSD = 0m,
                ReceiptImagePath = r.ReceiptImagePath,
                Notes = r.Notes
            });
        }

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
            Title = "سجل القبض المركزي",
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

