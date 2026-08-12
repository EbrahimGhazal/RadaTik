using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>تحديد شبكة الشركة (الأم) لعزل بيانات المستودع والدفتر والرواتب.</summary>
public static class CompanyBusinessScopeHelper
{
    public sealed record CompanyScope(int CompanyNetworkId, string CompanyNetworkName);

    public static async Task<CompanyScope?> ResolveAsync(
      HttpContext httpContext,
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      ClaimsPrincipal principal)
    {
        ApplicationUser? user = await userManager.GetUserAsync(principal);
        if (user == null || !user.NetworkId.HasValue)
        {
            return null;
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(httpContext, context, user);
        if (!selectedNetworkId.HasValue)
        {
            return null;
        }

        Network? selectedNetwork = await context.Networks
          .AsNoTracking()
          .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return null;
        }

        int companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        string companyName = selectedNetwork.Name;
        if (selectedNetwork.ParentNetworkId.HasValue)
        {
            companyName = await context.Networks
              .AsNoTracking()
              .Where(n => n.Id == companyNetworkId)
              .Select(n => n.Name)
              .FirstOrDefaultAsync() ?? companyName;
        }

        return new CompanyScope(companyNetworkId, companyName);
    }
}
