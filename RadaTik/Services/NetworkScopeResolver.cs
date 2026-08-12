using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services;

public interface INetworkScopeResolver
{
    Task ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default);
}

/// <summary>يملأ <see cref="ICurrentNetworkScope"/> من الجلسة والمستخدم الحالي.</summary>
public sealed class NetworkScopeResolver(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    ICurrentNetworkScope networkScope) : INetworkScopeResolver
{
    public async Task ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        networkScope.Reset();

        if (httpContext.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        ApplicationUser? user = await userManager.GetUserAsync(httpContext.User);
        if (user == null)
        {
            return;
        }

        if (httpContext.User.IsInRole(RoleNames.SystemAdministrator))
        {
            networkScope.SetScope(isFilterActive: true, bypassAllNetworks: true, Array.Empty<int>());
            return;
        }

        // نقطة التحصيل تعمل عبر الشركات/الشبكات — يجب أن ترى كل الشبكات غير المعطّلة
        if (httpContext.User.IsInRole(RoleNames.CollectionPoint))
        {
            networkScope.SetScope(isFilterActive: true, bypassAllNetworks: true, Array.Empty<int>());
            return;
        }

        IList<string> roles = await userManager.GetRolesAsync(user);
        List<int> networkIds = [];

        if (roles.Contains(RoleNames.NetworkAdministrator))
        {
            if (user.NetworkId.HasValue)
            {
                int mainId = user.NetworkId.Value;
                // IgnoreQueryFilters: بناء قائمة الشبكات المسموحة يجب ألا يعتمد على نطاق لم يُضبط بعد
                networkIds = await db.Networks
                    .IgnoreQueryFilters()
                    .AsNoTracking()
                    .Where(n => n.Id == mainId || n.ParentNetworkId == mainId)
                    .Select(n => n.Id)
                    .ToListAsync(cancellationToken);

                int? currentId = ResolveNetworkAdministratorCurrentId(httpContext, user, networkIds);
                if (currentId.HasValue && !networkIds.Contains(currentId.Value))
                {
                    networkIds = [currentId.Value];
                }
            }
            else
            {
                int? sessionOnly = httpContext.Session.GetInt32("SelectedNetworkId");
                if (sessionOnly.HasValue)
                {
                    networkIds = [sessionOnly.Value];
                }
            }
        }
        else
        {
            int? networkId = ResolveEmployeeCurrentNetworkId(httpContext, user);
            if (networkId.HasValue)
            {
                networkIds = [networkId.Value];
            }
        }

        if (networkIds.Count == 0)
        {
            networkScope.SetScope(isFilterActive: true, bypassAllNetworks: false, Array.Empty<int>());
            return;
        }

        networkScope.SetScope(isFilterActive: true, bypassAllNetworks: false, networkIds);
    }

    /// <summary>
    /// يعادل منطق <see cref="NetworkHelper.GetCurrentNetworkId"/> لمدير الشركة دون استعلام EF متزامن
    /// (الاستعلام المتزامن على نفس DbContext قبل async يسبب أخطاء وإلغاءات).
    /// </summary>
    private static int? ResolveNetworkAdministratorCurrentId(
        HttpContext httpContext,
        ApplicationUser user,
        IReadOnlyList<int> allowedNetworkIds)
    {
        int? sessionNetworkId = httpContext.Session.GetInt32("SelectedNetworkId");
        if (sessionNetworkId.HasValue)
        {
            if (user.NetworkId.HasValue && user.NetworkId.Value == sessionNetworkId.Value)
            {
                return sessionNetworkId.Value;
            }

            if (allowedNetworkIds.Contains(sessionNetworkId.Value))
            {
                return sessionNetworkId.Value;
            }

            httpContext.Session.Remove("SelectedNetworkId");
        }

        if (user.NetworkId.HasValue)
        {
            NetworkHelper.SetCurrentNetworkId(httpContext, user.NetworkId.Value);
            return user.NetworkId.Value;
        }

        return null;
    }

    private static int? ResolveEmployeeCurrentNetworkId(HttpContext httpContext, ApplicationUser user)
    {
        int? sessionNetworkId = httpContext.Session.GetInt32("SelectedNetworkId");
        if (sessionNetworkId.HasValue &&
            user.NetworkId.HasValue &&
            user.NetworkId.Value == sessionNetworkId.Value)
        {
            return sessionNetworkId.Value;
        }

        if (sessionNetworkId.HasValue && sessionNetworkId != user.NetworkId)
        {
            httpContext.Session.Remove("SelectedNetworkId");
        }

        return user.NetworkId;
    }
}
