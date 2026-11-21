using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.CompanySettings;

namespace RadaTik.Navigation;

public interface ICompanySidebarNavBuilder
{
    Task<SidebarNavViewModel> BuildAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        CompanySidebarRole role,
        CancellationToken ct = default);
}

public sealed class CompanySidebarNavBuilder : ICompanySidebarNavBuilder
{
    private readonly IFeatureAccessService _features;
    private readonly IPermissionService _permissions;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly LinkGenerator _links;
    private readonly CompanySettingsService _companySettings;

    public CompanySidebarNavBuilder(
        IFeatureAccessService features,
        IPermissionService permissions,
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        LinkGenerator links,
        CompanySettingsService companySettings)
    {
        _features = features;
        _permissions = permissions;
        _db = db;
        _userManager = userManager;
        _links = links;
        _companySettings = companySettings;
    }

    public async Task<SidebarNavViewModel> BuildAsync(
        ClaimsPrincipal user,
        HttpContext httpContext,
        CompanySidebarRole role,
        CancellationToken ct = default)
    {
        RouteValueDictionary routeData = httpContext.GetRouteData()?.Values ?? new RouteValueDictionary();
        string controller = routeData["controller"]?.ToString() ?? "";
        string action = routeData["action"]?.ToString() ?? "";
        string currentType = httpContext.Request.Query["type"].ToString();
        if (string.IsNullOrWhiteSpace(currentType))
        {
            currentType = routeData["type"]?.ToString() ?? "";
        }

        NavBuildContext ctx = await CreateContextAsync(user, httpContext, role, controller, action, currentType, ct);

        List<SidebarSectionVm> sections = [];
        bool anyWorkSection = false;

        foreach (NavSectionDef def in CompanySidebarCatalog.GetSections())
        {
            if (!AudienceMatches(def.Audience, role))
            {
                continue;
            }

            if (def.Key == "custom")
            {
                if (role == CompanySidebarRole.Employee &&
                    !await _permissions.HasPermissionAsync(user, "CustomServices.View"))
                {
                    continue;
                }

                SidebarSectionVm? custom = await BuildCustomServicesSectionAsync(ctx, httpContext, ct);
                if (custom != null)
                {
                    sections.Add(custom);
                }

                continue;
            }

            if (def.IsHeadingOnly)
            {
                // added later only if work sections exist
                continue;
            }

            List<SidebarEntryVm> entries = [];
            foreach (NavEntryDef entry in def.Entries)
            {
                if (!AudienceMatches(entry.Audience, role))
                {
                    continue;
                }

                if (entry.VisibleWhen != null && !entry.VisibleWhen(ctx))
                {
                    continue;
                }

                if (entry.IsDropdown)
                {
                    SidebarEntryVm? dd = await BuildDropdownAsync(entry, ctx, httpContext, role, ct);
                    if (dd != null)
                    {
                        entries.Add(dd);
                    }

                    continue;
                }

                if (entry.Link == null)
                {
                    continue;
                }

                SidebarLinkVm? link = await TryBuildLinkAsync(entry.Link, ctx, httpContext, role, isSub: false, ct);
                if (link != null)
                {
                    entries.Add(new SidebarEntryVm { Link = link });
                }
            }

            if (entries.Count == 0 && !def.Flat)
            {
                // allow flat home/account always if they have entries; skip empty toggle sections
                continue;
            }

            if (entries.Count == 0)
            {
                continue;
            }

            bool expanded = def.Key == "reports"
                || def.ExpandControllers.Any(c =>
                    string.Equals(c, controller, StringComparison.OrdinalIgnoreCase));
            bool hasActive = entries.Any(e =>
                (e.Link?.IsActive ?? false) ||
                e.Children.Any(c => c.IsActive));

            if (def.Key is "subscribers" or "support" or "ai" or "network" or "finance" or "erp" or "reports" or "team")
            {
                anyWorkSection = true;
            }

            sections.Add(new SidebarSectionVm
            {
                Key = def.Key,
                Title = def.Title,
                IconClass = def.IconClass,
                CssClass = def.CssClass,
                Flat = def.Flat,
                StaticTitle = def.StaticTitle,
                IsExpanded = expanded || hasActive || (role == CompanySidebarRole.Employee && !def.Flat && def.Key != "account"),
                HasActive = hasActive,
                Entries = entries
            });
        }

        if (role == CompanySidebarRole.Employee && anyWorkSection)
        {
            int insertAt = sections.FindIndex(s => s.Key != "home");
            if (insertAt < 0)
            {
                insertAt = sections.Count;
            }

            // after home
            int homeIdx = sections.FindIndex(s => s.Key == "home");
            insertAt = homeIdx >= 0 ? homeIdx + 1 : 0;
            sections.Insert(insertAt, new SidebarSectionVm
            {
                Key = "work-heading",
                Title = "مهامي وصلاحياتي",
                IconClass = "fas fa-briefcase",
                CssClass = "nav-section-heading",
                StaticTitle = true,
                IsHeadingOnly = true,
                Entries = []
            });
        }

        bool showEmpty = role == CompanySidebarRole.Employee
            && !anyWorkSection
            && !ctx.CanPayroll;

        string setupUrl = _links.GetPathByName(httpContext, "networkManager-company-settings-setup")
            ?? "/networkManager/companySettings/setup";

        return new SidebarNavViewModel
        {
            Role = role,
            Sections = sections,
            ShowEmptyWorkHint = showEmpty,
            CompanySettingsSetupIncomplete = ctx.CompanySettingsSetupIncomplete,
            CompanySettingsSetupUrl = setupUrl,
            PendingCompanySettingsApprovalsCount = ctx.PendingCompanySettingsApprovalsCount
        };
    }

    private async Task<NavBuildContext> CreateContextAsync(
        ClaimsPrincipal user,
        HttpContext http,
        CompanySidebarRole role,
        string controller,
        string action,
        string currentType,
        CancellationToken ct)
    {
        async Task<bool> Feat(string key) => await _features.HasFeatureAsync(user, http, key);

        int pending = 0;
        int pendingSettings = 0;
        bool setupIncomplete = false;
        if (role == CompanySidebarRole.Admin)
        {
            try
            {
                ApplicationUser? currentUser = await _userManager.GetUserAsync(user);
                int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(http, _db, currentUser);
                if (selectedNetworkId.HasValue)
                {
                    var selectedNetwork = await _db.Networks.AsNoTracking()
                        .Where(n => n.Id == selectedNetworkId.Value)
                        .Select(n => new { n.ParentNetworkId })
                        .FirstOrDefaultAsync(ct);
                    int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
                    var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);
                    pending = await _db.NetworkServiceRequests.AsNoTracking()
                        .Where(r => companyScope.Contains(r.NetworkId) &&
                                    r.Status == NetworkServiceRequestStatus.Pending &&
                                    ((r.Notes != null && r.Notes.StartsWith("EMP_REQ:")) ||
                                     (r.Notes != null && r.Notes.Contains("SECTOR_CREATE_PENDING:"))))
                        .CountAsync(ct);

                    setupIncomplete = !await _companySettings.IsSetupCompletedAsync(companyNetworkId, ct);
                    if (currentUser != null)
                    {
                        pendingSettings = await _companySettings.CountPendingApprovalsForUserAsync(
                            companyNetworkId, currentUser.Id, ct);
                    }
                }
            }
            catch
            {
                pending = 0;
                pendingSettings = 0;
                setupIncomplete = false;
            }
        }

        return new NavBuildContext
        {
            Role = role,
            Controller = controller,
            Action = action,
            CurrentType = currentType,
            Path = http.Request.Path.Value ?? "",
            CanNetworks = await Feat(FeatureKeys.Networks),
            CanMikroTik = await Feat(FeatureKeys.MikroTikServers),
            CanProfiles = await Feat(FeatureKeys.Profiles),
            CanUsers = await Feat(FeatureKeys.Users),
            CanClients = await Feat(FeatureKeys.Clients),
            CanSectors = await Feat(FeatureKeys.Sectors),
            CanReceivers = await Feat(FeatureKeys.Receivers),
            CanRequests = await Feat(FeatureKeys.Requests),
            CanReports = await Feat(FeatureKeys.Reports),
            CanWarehouse = await Feat(FeatureKeys.Warehouse),
            CanMoneyDiary = await Feat(FeatureKeys.MoneyDiary),
            CanPayroll = await Feat(FeatureKeys.Payroll),
            CanErp = await Feat(FeatureKeys.Erp),
            IsNetworkAdmin = user.IsInRole(RoleNames.NetworkAdministrator),
            PendingEmployeeApprovalsCount = pending,
            PendingCompanySettingsApprovalsCount = pendingSettings,
            CompanySettingsSetupIncomplete = setupIncomplete
        };
    }

    private async Task<SidebarEntryVm?> BuildDropdownAsync(
        NavEntryDef entry,
        NavBuildContext ctx,
        HttpContext http,
        CompanySidebarRole role,
        CancellationToken ct)
    {
        List<SidebarLinkVm> children = [];

        foreach (NavLinkDef child in entry.Children)
        {
            if (child.Key.StartsWith("label:", StringComparison.Ordinal))
            {
                children.Add(new SidebarLinkVm
                {
                    Title = child.Title,
                    Href = "#",
                    IconClass = "",
                    IsSub = true,
                    ExtraCssClass = "nav-group-label-item"
                });
                continue;
            }

            SidebarLinkVm? link = await TryBuildLinkAsync(child, ctx, http, role, isSub: true, ct);
            if (link != null)
            {
                children.Add(link);
            }
        }

        // Remove placeholder label-only items that use ExtraCssClass — render as group labels in view
        // Filter: keep group-label markers only if followed by real links
        List<SidebarLinkVm> cleaned = [];
        for (int i = 0; i < children.Count; i++)
        {
            SidebarLinkVm c = children[i];
            if (c.ExtraCssClass == "nav-group-label-item")
            {
                bool hasFollowing = children.Skip(i + 1).Any(x => x.ExtraCssClass != "nav-group-label-item");
                if (hasFollowing)
                {
                    cleaned.Add(c);
                }

                continue;
            }

            cleaned.Add(c);
        }

        if (cleaned.All(c => c.ExtraCssClass == "nav-group-label-item") || cleaned.Count == 0)
        {
            return null;
        }

        bool open = entry.DropdownOpenControllers.Any(c =>
            string.Equals(c, ctx.Controller, StringComparison.OrdinalIgnoreCase))
            || cleaned.Any(c => c.IsActive);

        return new SidebarEntryVm
        {
            IsDropdown = true,
            DropdownTitle = entry.DropdownTitle,
            DropdownIcon = entry.DropdownIcon,
            DropdownOpen = open,
            Children = cleaned
        };
    }

    private async Task<SidebarLinkVm?> TryBuildLinkAsync(
        NavLinkDef def,
        NavBuildContext ctx,
        HttpContext http,
        CompanySidebarRole role,
        bool isSub,
        CancellationToken ct)
    {
        if (!AudienceMatches(def.Audience, role))
        {
            return null;
        }

        if (def.VisibleWhen != null && !def.VisibleWhen(ctx))
        {
            return null;
        }

        if (def.RequiresNetworkAdminRole && !ctx.IsNetworkAdmin)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(def.FeatureKey))
        {
            bool ok = def.FeatureKey switch
            {
                FeatureKeys.Networks => ctx.CanNetworks,
                FeatureKeys.MikroTikServers => ctx.CanMikroTik,
                FeatureKeys.Profiles => ctx.CanProfiles,
                FeatureKeys.Users => ctx.CanUsers,
                FeatureKeys.Clients => ctx.CanClients,
                FeatureKeys.Sectors => ctx.CanSectors,
                FeatureKeys.Receivers => ctx.CanReceivers,
                FeatureKeys.Requests => ctx.CanRequests,
                FeatureKeys.Reports => ctx.CanReports,
                FeatureKeys.Warehouse => ctx.CanWarehouse,
                FeatureKeys.MoneyDiary => ctx.CanMoneyDiary,
                FeatureKeys.Payroll => ctx.CanPayroll,
                FeatureKeys.Erp => ctx.CanErp,
                _ => await _features.HasFeatureAsync(http.User, http, def.FeatureKey)
            };
            if (!ok)
            {
                return null;
            }
        }

        if (role == CompanySidebarRole.Employee && !string.IsNullOrWhiteSpace(def.PermissionKey))
        {
            bool allowed = false;
            foreach (string part in def.PermissionKey.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (await _permissions.HasPermissionAsync(http.User, part))
                {
                    allowed = true;
                    break;
                }
            }

            if (!allowed)
            {
                return null;
            }
        }

        string? href = ResolveHref(def, http);
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        bool active = IsActive(def, ctx);
        int? badgeCount = null;
        if (def.UsePendingApprovalsBadge && ctx.PendingEmployeeApprovalsCount > 0)
        {
            badgeCount = ctx.PendingEmployeeApprovalsCount;
        }
        else if (def.UseCompanySettingsApprovalsBadge && ctx.PendingCompanySettingsApprovalsCount > 0)
        {
            badgeCount = ctx.PendingCompanySettingsApprovalsCount;
        }

        return new SidebarLinkVm
        {
            Title = def.Title,
            Href = href,
            IconClass = def.IconClass,
            IsActive = active,
            Featured = def.Featured,
            IsSub = isSub,
            BadgeDomId = def.BadgeDomId,
            BadgeCount = badgeCount,
            Hint = def.Hint,
            ExtraCssClass = def.ExtraCssClass
        };
    }

    private async Task<SidebarSectionVm?> BuildCustomServicesSectionAsync(
        NavBuildContext ctx,
        HttpContext http,
        CancellationToken ct)
    {
        List<SystemService> services;
        try
        {
            services = await _db.SystemServices.AsNoTracking()
                .Where(s => s.IsActive && s.Key.StartsWith("CUSTOM:"))
                .OrderBy(s => s.DisplayName)
                .ToListAsync(ct);
        }
        catch
        {
            return null;
        }

        if (services.Count == 0)
        {
            return null;
        }

        List<SidebarEntryVm> entries = [];
        foreach (SystemService s in services)
        {
            string? href = _links.GetPathByName(http, "networkManager-customService-index", new { serviceKey = s.Key });
            if (string.IsNullOrWhiteSpace(href))
            {
                continue;
            }

            bool active = string.Equals(ctx.Controller, "CustomServices", StringComparison.OrdinalIgnoreCase)
                && ctx.Path.Contains(s.Key, StringComparison.OrdinalIgnoreCase);

            entries.Add(new SidebarEntryVm
            {
                Link = new SidebarLinkVm
                {
                    Title = s.DisplayName,
                    Href = href,
                    IconClass = string.IsNullOrWhiteSpace(s.IconClass) ? "fas fa-layer-group" : s.IconClass,
                    IsActive = active
                }
            });
        }

        if (entries.Count == 0)
        {
            return null;
        }

        return new SidebarSectionVm
        {
            Key = "custom",
            Title = "خدمات إضافية",
            IconClass = "fas fa-layer-group",
            IsExpanded = entries.Any(e => e.Link?.IsActive == true),
            Entries = entries
        };
    }

    private string? ResolveHref(NavLinkDef def, HttpContext http)
    {
        RouteValueDictionary values = new();
        if (def.RouteValues != null)
        {
            foreach (KeyValuePair<string, string?> kv in def.RouteValues)
            {
                values[kv.Key] = kv.Value;
            }
        }

        if (!string.IsNullOrWhiteSpace(def.RouteName))
        {
            return _links.GetPathByName(http, def.RouteName, values);
        }

        if (!string.IsNullOrWhiteSpace(def.Controller))
        {
            values["area"] = def.Area ?? "";
            values["controller"] = def.Controller;
            values["action"] = def.Action ?? "Index";
            return _links.GetPathByAction(http, def.Action ?? "Index", def.Controller, values);
        }

        return null;
    }

    private static bool IsActive(NavLinkDef def, NavBuildContext ctx)
    {
        if (def.ActiveControllers.Length == 0)
        {
            return false;
        }

        if (!def.ActiveControllers.Any(c => string.Equals(c, ctx.Controller, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(def.ActiveQueryType))
        {
            return string.Equals(def.ActiveQueryType, ctx.CurrentType, StringComparison.OrdinalIgnoreCase);
        }

        if (def.ExcludeActions is { Length: > 0 } &&
            def.ExcludeActions.Any(a => string.Equals(a, ctx.Action, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        if (def.ActiveActions is { Length: > 0 })
        {
            return def.ActiveActions.Any(a => string.Equals(a, ctx.Action, StringComparison.OrdinalIgnoreCase));
        }

        return true;
    }

    private static bool AudienceMatches(NavAudience audience, CompanySidebarRole role) =>
        role == CompanySidebarRole.Admin
            ? audience.HasFlag(NavAudience.Admin)
            : audience.HasFlag(NavAudience.Employee);
}
