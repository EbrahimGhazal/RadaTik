using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Models.Business;
using RadaTik.Services;
using RadaTik.ViewModels;

namespace RadaTik.ViewComponents;

/// <summary>
/// عرض الرصيد في الهيدر بجانب اسم المستخدم حسب الدور.
/// </summary>
public class WalletBalanceViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CompanyPayrollService _payrollService;

    public WalletBalanceViewComponent(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CompanyPayrollService payrollService)
    {
        _context = context;
        _userManager = userManager;
        _payrollService = payrollService;
    }

    public async Task<IViewComponentResult> InvokeAsync(string layout = "inline")
    {
        if (!User.Identity?.IsAuthenticated ?? true)
        {
            return View<HeaderWalletBalanceViewModel?>(null);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null)
        {
            return View<HeaderWalletBalanceViewModel?>(null);
        }

        HeaderWalletBalanceViewModel? model = null;

        if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
        {
            EmployeePayrollSelfHelper.SelfPayrollContext? self =
                await EmployeePayrollSelfHelper.ResolveSelfPayrollAsync(_context, user);
            if (self != null)
            {
                model = new HeaderWalletBalanceViewModel
                {
                    BalanceSyp = self.Employee.WalletBalance,
                    ShowDualCurrency = false,
                    BalanceLabel = "محفظتي",
                    WalletUrl = "/employee/my-payroll"
                };
            }
        }
        else if (User.IsInRole(RoleNames.NetworkAdministrator))
        {
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (networkId.HasValue)
            {
                Network? network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                int effectiveNetworkId = network?.ParentNetworkId ?? networkId.Value;
                Network? effectiveNetwork = effectiveNetworkId != networkId.Value
                    ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
                    : network;

                if (effectiveNetwork != null)
                {
                    model = new HeaderWalletBalanceViewModel
                    {
                        BalanceSyp = effectiveNetwork.Balance,
                        BalanceUsd = effectiveNetwork.BalanceUsd,
                        ShowDualCurrency = true,
                        WalletUrl = "/networkManager/wallet"
                    };
                }
            }
        }
        else if (User.IsInRole(RoleNames.CollectionPoint))
        {
            CollectionPointAccount? account = await _context.CollectionPointAccounts.AsNoTracking()
                .FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (account != null)
            {
                model = new HeaderWalletBalanceViewModel
                {
                    BalanceSyp = account.Balance,
                    WalletUrl = "/CollectionPoint/Wallet/TopUp"
                };
            }
        }
        else if (User.IsInRole(RoleNames.Client) && user.ClientId.HasValue)
        {
            Client? client = await _context.Clients.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == user.ClientId.Value);
            if (client != null)
            {
                model = new HeaderWalletBalanceViewModel
                {
                    BalanceSyp = client.Balance,
                    WalletUrl = "/ClientPortal/RequestTopUp"
                };
            }
        }

        if (model != null && string.Equals(layout, "stacked", StringComparison.OrdinalIgnoreCase))
        {
            model.StackCurrencies = true;
        }

        return View(model);
    }
}
