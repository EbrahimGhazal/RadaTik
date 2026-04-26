namespace RadTik.Security;

/// <summary>
/// Centralized role names to avoid string duplication and typos.
/// Keep legacy roles for backward compatibility during migration.
/// </summary>
public static class RoleNames
{
    public const string SystemAdministrator = "SystemAdministrator";

    /// <summary>
    /// Company admin (network administrator).
    /// </summary>
    public const string NetworkAdministrator = "NetworkAdministrator";

    /// <summary>
    /// Employee tied to a specific company/network.
    /// </summary>
    public const string CompanyEmployee = "CompanyEmployee";

    /// <summary>
    /// Employee tied to the system (future: global support / NOC).
    /// </summary>
    public const string SystemEmployee = "SystemEmployee";

    public const string CollectionPoint = "CollectionPoint";
    public const string Client = "Client";

    /// <summary>
    /// Legacy role used in the current app. Kept temporarily to avoid breaking existing accounts.
    /// Treat as CompanyEmployee until migration is completed.
    /// </summary>
    public const string EmployeeLegacy = "Employee";
}

