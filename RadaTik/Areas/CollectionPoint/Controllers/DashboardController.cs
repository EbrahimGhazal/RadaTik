using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.ViewModels.CollectionPoint;

namespace RadaTik.Areas.CollectionPoint.Controllers;

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
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        string userId = user.Id;
        List<NetworkCardItem> networks = await LoadAvailableNetworkCardsAsync();

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            CollectionPointAccount? acc = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == userId);
            if (acc?.NetworkId != null)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, acc.NetworkId.Value);
                networkId = acc.NetworkId;
            }
        }

        if (!networkId.HasValue)
        {
            CollectionPointAccount? standaloneAccount = await _context.CollectionPointAccounts
                .FirstOrDefaultAsync(a => a.UserId == userId && a.NetworkId == null);
            if (standaloneAccount == null)
            {
                standaloneAccount = new CollectionPointAccount
                {
                    UserId = userId,
                    NetworkId = null,
                    Balance = 0m,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.CollectionPointAccounts.Add(standaloneAccount);
                await _context.SaveChangesAsync();
            }

            CollectionPointDashboardViewModel standaloneModel = new CollectionPointDashboardViewModel
            {
                Query = q,
                AccountBalance = standaloneAccount.Balance,
                Clients = [],
                Networks = networks,
                RecentTransactions = await _context.PaymentTransactions
                    .Where(t => t.ReceivedByUserId == userId)
                    .Include(t => t.Client)
                    .OrderByDescending(t => t.PaymentDate)
                    .Take(20)
                    .ToListAsync()
            };

            if (networks.Count == 0)
            {
                TempData["Info"] = "لا توجد شركات/شبكات نشطة متاحة حالياً. يرجى التواصل مع مدير النظام.";
            }
            else
            {
                TempData["Info"] = "اختر شركة/شبكة من البطاقات للبحث عن المشتركين وتسديد الفواتير.";
            }

            return View("~/Areas/CollectionPoint/Views/CollectionPoint/Index.cshtml", standaloneModel);
        }

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .Include(a => a.Network)
            .FirstOrDefaultAsync(a => a.UserId == userId);

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = userId,
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

        IQueryable<Client> clientsQuery = _context.Clients
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

        List<Client> clients = await clientsQuery.Take(50).ToListAsync();

        List<PaymentTransaction> recentTransactions = await _context.PaymentTransactions
            .Where(t => t.ReceivedByUserId == userId)
            .Include(t => t.Client)
            .OrderByDescending(t => t.PaymentDate)
            .Take(20)
            .ToListAsync();

        CollectionPointDashboardViewModel model = new CollectionPointDashboardViewModel
        {
            Query = q,
            NetworkId = networkId.Value,
            NetworkName = account.Network?.Name,
            AccountBalance = account.Balance,
            Clients = clients,
            Networks = networks,
            RecentTransactions = recentTransactions
        };

        return View("~/Areas/CollectionPoint/Views/CollectionPoint/Index.cshtml", model);
    }

    /// <summary>
    /// الشركات/الشبكات المتاحة لنقطة التحصيل (النشطة + قيد الإنشاء؛ نستبعد المعطّلة فقط).
    /// </summary>
    private async Task<List<NetworkCardItem>> LoadAvailableNetworkCardsAsync()
    {
        List<Network> networks = await _context.Networks
            .Where(n => n.Status != NetworkStatus.Inactive)
            .Include(n => n.ManagerUser)
            .Include(n => n.ParentNetwork)
            .OrderBy(n => n.ParentNetworkId.HasValue)
            .ThenBy(n => n.Name)
            .ToListAsync();

        return networks.Select(n => new NetworkCardItem
        {
            Id = n.Id,
            Name = n.ParentNetworkId.HasValue && n.ParentNetwork != null
                ? $"{n.ParentNetwork.Name} — {n.Name}"
                : n.Name,
            LogoPath = n.LogoPath ?? n.ParentNetwork?.LogoPath,
            Phone = n.ManagerUser?.PhoneNumber ?? n.ManagerUser?.UserName
        }).ToList();
    }
}
