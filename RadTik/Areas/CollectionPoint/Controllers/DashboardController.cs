using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.CollectionPoint;

namespace RadTik.Areas.CollectionPoint.Controllers;

[Area("CollectionPoint")]
[Authorize(Roles = $"{RoleNames.CollectionPoint},{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    /// <summary>
    /// لوحة تحكم نقطة التحصيل. عرض المحتوى مباشرة دون إعادة توجيه لتجنب ERR_TOO_MANY_REDIRECTS.
    /// </summary>
    public async Task<IActionResult> Index(string? q = null)
    {
        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue && user != null)
        {
            var acc = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (acc?.NetworkId != null)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, acc.NetworkId.Value);
                networkId = acc.NetworkId;
            }
        }

        if (user == null || !networkId.HasValue)
        {
            TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
            return View("~/Areas/CollectionPoint/Views/CollectionPoint/Index.cshtml", new CollectionPointDashboardViewModel());
        }

        var account = await _context.CollectionPointAccounts
            .Include(a => a.Network)
            .FirstOrDefaultAsync(a => a.UserId == user.Id);

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = user.Id,
                NetworkId = networkId.Value,
                Balance = 0m,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.CollectionPointAccounts.Add(account);
            await _context.SaveChangesAsync();
        }
        else if (account.NetworkId != networkId.Value)
        {
            account.NetworkId = networkId.Value;
            account.UpdatedAt = DateTime.Now;
            await _context.SaveChangesAsync();
        }

        var clientsQuery = _context.Clients
            .Where(c => c.NetworkId == networkId.Value)
            .OrderByDescending(c => c.Id)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            clientsQuery = clientsQuery.Where(c =>
                (c.Name != null && c.Name.Contains(q)) ||
                (c.UserName != null && c.UserName.Contains(q)) ||
                (c.SID != null && c.SID.Contains(q)) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(q)));
        }

        var clients = await clientsQuery.Take(50).ToListAsync();

        var recentTransactions = await _context.PaymentTransactions
            .Where(t => t.ReceivedByUserId == user.Id)
            .Include(t => t.Client)
            .OrderByDescending(t => t.PaymentDate)
            .Take(20)
            .ToListAsync();

        var model = new CollectionPointDashboardViewModel
        {
            Query = q,
            NetworkId = networkId.Value,
            NetworkName = account.Network?.Name,
            AccountBalance = account.Balance,
            Clients = clients,
            RecentTransactions = recentTransactions
        };

        return View("~/Areas/CollectionPoint/Views/CollectionPoint/Index.cshtml", model);
    }
}

