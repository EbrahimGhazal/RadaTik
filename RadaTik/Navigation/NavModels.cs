namespace RadaTik.Navigation;

public enum CompanySidebarRole
{
    Admin,
    Employee
}

[Flags]
public enum NavAudience
{
    Admin = 1,
    Employee = 2,
    Both = Admin | Employee
}

public sealed class SidebarNavViewModel
{
    public required CompanySidebarRole Role { get; init; }
    public required IReadOnlyList<SidebarSectionVm> Sections { get; init; }
    public bool ShowEmptyWorkHint { get; init; }
    /// <summary>true إذا لم تكتمل تهيئة إعدادات الشركة بعد.</summary>
    public bool CompanySettingsSetupIncomplete { get; init; }
    public string CompanySettingsSetupUrl { get; init; } = "/networkManager/companySettings/setup";
    public int PendingCompanySettingsApprovalsCount { get; init; }
}

public sealed class SidebarSectionVm
{
    public required string Key { get; init; }
    public string? Title { get; init; }
    public string? IconClass { get; init; }
    public string? CssClass { get; init; }
    public bool Flat { get; init; }
    public bool StaticTitle { get; init; }
    public bool IsHeadingOnly { get; init; }
    public bool IsExpanded { get; init; }
    public bool HasActive { get; init; }
    public required IReadOnlyList<SidebarEntryVm> Entries { get; init; }
}

public sealed class SidebarEntryVm
{
    public bool IsGroupLabel { get; init; }
    public string? GroupLabelText { get; init; }

    public bool IsDropdown { get; init; }
    public string? DropdownTitle { get; init; }
    public string? DropdownIcon { get; init; }
    public bool DropdownOpen { get; init; }
    public IReadOnlyList<SidebarLinkVm> Children { get; init; } = [];

    public SidebarLinkVm? Link { get; init; }
}

public sealed class SidebarLinkVm
{
    public required string Title { get; init; }
    public required string Href { get; init; }
    public required string IconClass { get; init; }
    public bool IsActive { get; init; }
    public bool Featured { get; init; }
    public bool IsSub { get; init; }
    public string? BadgeDomId { get; init; }
    public int? BadgeCount { get; init; }
    public string? Hint { get; init; }
    public string? ExtraCssClass { get; init; }
}

/// <summary>تعريف ثابت في الكتالوج قبل التصفية وحل الروابط.</summary>
public sealed class NavSectionDef
{
    public required string Key { get; init; }
    public string? Title { get; init; }
    public string? IconClass { get; init; }
    public string? CssClass { get; init; }
    public bool Flat { get; init; }
    public bool StaticTitle { get; init; }
    public bool IsHeadingOnly { get; init; }
    public NavAudience Audience { get; init; } = NavAudience.Both;
    public string[] ExpandControllers { get; init; } = [];
    public required IReadOnlyList<NavEntryDef> Entries { get; init; }
}

public sealed class NavEntryDef
{
    public bool IsGroupLabel { get; init; }
    public string? GroupLabelText { get; init; }

    public bool IsDropdown { get; init; }
    public string? DropdownTitle { get; init; }
    public string? DropdownIcon { get; init; }
    public string[] DropdownOpenControllers { get; init; } = [];

    public NavLinkDef? Link { get; init; }
    public IReadOnlyList<NavLinkDef> Children { get; init; } = [];

    public NavAudience Audience { get; init; } = NavAudience.Both;
    public Func<NavBuildContext, bool>? VisibleWhen { get; init; }
}

public sealed class NavLinkDef
{
    public required string Key { get; init; }
    public required string Title { get; init; }
    public required string IconClass { get; init; }
    public NavAudience Audience { get; init; } = NavAudience.Both;

    public string? FeatureKey { get; init; }
    public string? PermissionKey { get; init; }
    public bool RequiresNetworkAdminRole { get; init; }

    public string? RouteName { get; init; }
    public string? Area { get; init; }
    public string? Controller { get; init; }
    public string? Action { get; init; }
    public IReadOnlyDictionary<string, string?>? RouteValues { get; init; }

    public string[] ActiveControllers { get; init; } = [];
    public string[]? ActiveActions { get; init; }
    public string[]? ExcludeActions { get; init; }
    public string? ActiveQueryType { get; init; }

    public bool Featured { get; init; }
    public string? BadgeDomId { get; init; }
    public bool UsePendingApprovalsBadge { get; init; }
    public bool UseCompanySettingsApprovalsBadge { get; init; }
    public string? Hint { get; init; }
    public string? ExtraCssClass { get; init; }

    public Func<NavBuildContext, bool>? VisibleWhen { get; init; }
}

public sealed class NavBuildContext
{
    public required CompanySidebarRole Role { get; init; }
    public required string Controller { get; init; }
    public required string Action { get; init; }
    public required string CurrentType { get; init; }
    public required string Path { get; init; }

    public bool CanNetworks { get; init; }
    public bool CanMikroTik { get; init; }
    public bool CanProfiles { get; init; }
    public bool CanUsers { get; init; }
    public bool CanClients { get; init; }
    public bool CanSectors { get; init; }
    public bool CanReceivers { get; init; }
    public bool CanRequests { get; init; }
    public bool CanReports { get; init; }
    public bool CanWarehouse { get; init; }
    public bool CanMoneyDiary { get; init; }
    public bool CanPayroll { get; init; }
    public bool CanErp { get; init; }

    public bool IsNetworkAdmin { get; init; }
    public int PendingEmployeeApprovalsCount { get; init; }
    public int PendingCompanySettingsApprovalsCount { get; init; }
    public bool CompanySettingsSetupIncomplete { get; init; }

    public bool HasCompanyBooks => CanWarehouse || CanMoneyDiary || CanPayroll;
    public bool HasInfrastructure => CanNetworks || CanProfiles || CanMikroTik || CanSectors || CanReceivers;
}
