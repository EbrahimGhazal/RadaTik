using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using System.Security.Claims;

namespace RadaTik.Services;

/// <summary>
/// يتحقق من اشتراكات الشركة الفعّالة (أو الميزات القديمة في NetworkFeatures).
/// مدير النظام ومدير الشركة: جميع الخدمات متاحة دون قيود اشتراك على مستوى الوصول.
/// </summary>
public sealed class FeatureAccessService(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager) : IFeatureAccessService
{
    public async Task<bool> HasFeatureAsync(ClaimsPrincipal principal, HttpContext httpContext, string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return false;
        }

        if (principal?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        if (principal.IsInRole(RoleNames.SystemAdministrator) ||
            principal.IsInRole(RoleNames.NetworkAdministrator))
        {
            return true;
        }

        string? userId = userManager.GetUserId(principal);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return false;
        }

        string cacheKey = $"Feature::{userId}::{featureKey}";
        if (httpContext.Items.TryGetValue(cacheKey, out object? cachedObj) && cachedObj is bool cachedBool)
        {
            return cachedBool;
        }

        ApplicationUser? user = await userManager.GetUserAsync(principal);
        if (user == null)
        {
            httpContext.Items[cacheKey] = false;
            return false;
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(httpContext, context, user);
        if (!selectedNetworkId.HasValue && user.NetworkId.HasValue)
        {
            selectedNetworkId = user.NetworkId.Value;
        }

        int? effectiveCompanyNetworkId = await CompanyServiceEntitlementResolver.ResolveEffectiveCompanyNetworkIdAsync(
            context,
            selectedNetworkId);

        bool allowed = effectiveCompanyNetworkId.HasValue &&
                       await CompanyServiceEntitlementResolver.HasEntitlementAsync(
                           context,
                           effectiveCompanyNetworkId.Value,
                           featureKey);

        httpContext.Items[cacheKey] = allowed;
        return allowed;
    }
}
