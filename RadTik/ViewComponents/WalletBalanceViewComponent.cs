using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.ViewComponents;

/// <summary>
/// عرض الرصيد في الهيدر بجانب اسم المستخدم حسب الدور.
/// - مدير الشركة: رصيد الشبكة (Network.Balance)
/// - نقطة التحصيل: رصيد نقطة التحصيل (CollectionPointAccount.Balance)
/// - العميل: رصيد العميل (Client.Balance)
/// - مدير النظام: لا يوجد محفظة حالياً (null)
/// </summary>
public class WalletBalanceViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public WalletBalanceViewComponent(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return View<decimal?>(null);
        }

        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null)
        {
            return View<decimal?>(null);
        }

        decimal? balance = null;

        if (User.IsInRole(RoleNames.NetworkAdministrator))
        {
            // رصيد مدير الشركة = رصيد الشبكة الفعالة (الرئيسية)
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (networkId.HasValue)
            {
                var network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var effectiveNetworkId = network?.ParentNetworkId ?? networkId.Value;
                var effectiveNetwork = effectiveNetworkId != networkId.Value
                    ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
                    : network;
                balance = effectiveNetwork?.Balance ?? 0m;
            }
        }
        else if (User.IsInRole(RoleNames.CollectionPoint))
        {
            // رصيد نقطة التحصيل
            var account = await _context.CollectionPointAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == user.Id);
            balance = account?.Balance ?? 0m;
        }
        else if ((User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy)) && user.NetworkId.HasValue)
        {
            // رصيد الموظف = رصيد الشركة الفعّالة (الرئيسية)
            var network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == user.NetworkId.Value);
            var effectiveNetworkId = network?.ParentNetworkId ?? user.NetworkId.Value;
            var effectiveNetwork = effectiveNetworkId == user.NetworkId.Value
                ? network
                : await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId);
            balance = effectiveNetwork?.Balance ?? 0m;
        }
        else if (User.IsInRole(RoleNames.Client) && user.ClientId.HasValue)
        {
            // رصيد العميل
            var client = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == user.ClientId.Value);
            balance = client?.Balance ?? 0m;
        }
        // مدير النظام: لا توجد محفظة حالياً (balance يبقى null)

        return View(balance);
    }
}
