using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.SystemAdmin;

namespace RadTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class FundingRequestsController : Controller
{
    private readonly ApplicationDbContext _context;

    public FundingRequestsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        string? tab = null,
        NetworkTopUpRequestStatus? companyStatus = null,
        CollectionPointTopUpStatus? collectionPointStatus = null)
    {
        ViewData["Title"] = "طلبات تغذية الرصيد";

        var activeTab = (tab?.Trim().ToLowerInvariant()) switch
        {
            "collectionpoints" => FundingRequestsTab.CollectionPoints,
            "collectionpointsrequests" => FundingRequestsTab.CollectionPoints,
            "collectionpoint" => FundingRequestsTab.CollectionPoints,
            "points" => FundingRequestsTab.CollectionPoints,
            _ => FundingRequestsTab.Companies
        };

        var pendingCompanies = await _context.NetworkTopUpRequests
            .CountAsync(r => r.Status == NetworkTopUpRequestStatus.Pending);

        var pendingPoints = await _context.CollectionPointTopUpRequests
            .CountAsync(r =>
                r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin &&
                r.Status == CollectionPointTopUpStatus.Pending);

        var vm = new FundingRequestsIndexViewModel
        {
            ActiveTab = activeTab,
            CompanyStatus = companyStatus,
            CollectionPointStatus = collectionPointStatus,
            PendingCompaniesCount = pendingCompanies,
            PendingCollectionPointsCount = pendingPoints
        };

        if (activeTab == FundingRequestsTab.Companies)
        {
            var q = _context.NetworkTopUpRequests
                .AsNoTracking()
                .Include(r => r.Network)
                .Include(r => r.PaymentMethod)
                .Include(r => r.RequestedByUser)
                .Include(r => r.DecidedByUser)
                .AsQueryable();

            if (companyStatus.HasValue)
                q = q.Where(r => r.Status == companyStatus.Value);

            vm.CompanyItems = await q
                .OrderBy(r => r.Status == NetworkTopUpRequestStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.RequestedAt)
                .Take(1000)
                .ToListAsync();
        }
        else
        {
            var q = _context.CollectionPointTopUpRequests
                .AsNoTracking()
                .Include(r => r.CollectionPointAccount)
                    .ThenInclude(a => a!.User)
                .Include(r => r.CollectionPointAccount)
                    .ThenInclude(a => a!.Network)
                .Include(r => r.PaymentMethod)
                .Include(r => r.RequestedByUser)
                .Include(r => r.ProcessedByUser)
                .Where(r => r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin)
                .AsQueryable();

            if (collectionPointStatus.HasValue)
                q = q.Where(r => r.Status == collectionPointStatus.Value);

            vm.CollectionPointItems = await q
                .OrderBy(r => r.Status == CollectionPointTopUpStatus.Pending ? 0 : 1)
                .ThenByDescending(r => r.RequestedAt)
                .Take(1000)
                .ToListAsync();
        }

        return View(vm);
    }
}

