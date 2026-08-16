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
/// الموظف: يُسمح بالخدمة إذا مُنحت له صلاحية ضمن مصفوفة الموظف، أو إذا كانت الشركة مشتركة في الخدمة.
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

        // الموظف الذي مُنح صلاحيات من مدير الشركة يجب أن يصل للخدمة دون الاعتماد فقط على الاشتراك
        if (principal.IsInRole(RoleNames.CompanyEmployee) || principal.IsInRole(RoleNames.EmployeeLegacy))
        {
            IReadOnlyList<string> permissionKeys =
                EmployeeServicePermissionMatrix.GetPermissionKeysForFeature(featureKey);

            if (permissionKeys.Count > 0)
            {
                bool hasGrantedPermission = await (
                    from up in context.UserPermissions.AsNoTracking()
                    join p in context.Permissions.AsNoTracking() on up.PermissionId equals p.Id
                    where up.UserId == userId && permissionKeys.Contains(p.Key)
                    select up.Id).AnyAsync();

                if (hasGrantedPermission)
                {
                    httpContext.Items[cacheKey] = true;
                    return true;
                }
            }
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
