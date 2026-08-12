using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyAdmin.ViewComponents;

/// <summary>
/// يعرض نافذة غير قابلة للإغلاق عند وجود اشتراكات معلّقة بسبب عدم كفاية الرصيد عند موعد التجديد.
/// </summary>
public sealed class CompanyWalletInterventionViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public CompanyWalletInterventionViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        string? area = ViewContext.RouteData.Values["area"]?.ToString();
        if (!string.Equals(area, "CompanyAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return Content(string.Empty);
        }

        string? controller = ViewContext.RouteData.Values["controller"]?.ToString();
        string? action = ViewContext.RouteData.Values["action"]?.ToString();
        if (string.Equals(controller, "Wallet", StringComparison.OrdinalIgnoreCase))
        {
            return Content(string.Empty);
        }

        if (string.Equals(controller, "Network", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(action, "Create", StringComparison.OrdinalIgnoreCase))
        {
            return Content(string.Empty);
        }

        if (User?.Identity?.IsAuthenticated != true)
        {
            return Content(string.Empty);
        }

        if (!User.IsInRole(RoleNames.NetworkAdministrator))
        {
            return Content(string.Empty);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(HttpContext.User);
        if (user?.NetworkId is not { } networkId)
        {
            return Content(string.Empty);
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            return Content(string.Empty);
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return Content(string.Empty);
        }

        int companyId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        DateTime now = DateTime.Now;

        int suspendedRenewalCount = await _db.NetworkServiceSubscriptions
            .AsNoTracking()
            .CountAsync(s =>
                s.NetworkId == companyId &&
                s.Status == NetworkServiceSubscriptionStatus.Suspended &&
                s.BillingPeriod != PricingBillingPeriod.OneTime &&
                s.ExpiresAt <= now);

        if (suspendedRenewalCount == 0)
        {
            return Content(string.Empty);
        }

        string topUpUrl = Url.Action("TopUp", "Wallet", new { area = "CompanyAdmin" }) ?? "/networkManager/wallet/topup";

        CompanyWalletInterventionViewModel vm = new CompanyWalletInterventionViewModel
        {
            ShowModal = true,
            SuspendedRenewalCount = suspendedRenewalCount,
            TopUpUrl = topUpUrl
        };

        return View(vm);
    }
}
