namespace RadTik.Services;

public class ExpiredAccountsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CheckDate { get; set; }
    public int ExpiredAccountsFound { get; set; }
    public int DisabledInMikroTik { get; set; }
    public List<ExpiredAccountInfo> DisabledAccounts { get; set; } = new();
    public List<string> Errors { get; set; } = new();
}

public class ExpiredAccountInfo
{
    public int ClientId { get; set; }
    public string? ClientName { get; set; }
    public string? UserName { get; set; }
    public DateTime ExpirationDate { get; set; }
}

public class ImportUsersResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int AddedCount { get; set; }
    public int ExistingCount { get; set; }
    public int FailedCount { get; set; }
    public int UsersCreatedCount { get; set; }
    public int UsersFailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ImportUsersPreviewResult
{
    public int TotalUsersOnServer { get; set; }
    public int ImportableUsersCount { get; set; }
    public int ExistingUsersCount { get; set; }
    public int MissingProfileCount { get; set; }
    public int InvalidUsersCount { get; set; }
}

public class ImportProfilesPreviewResult
{
    public int TotalProfilesOnServer { get; set; }
    public int ImportableProfilesCount { get; set; }
    public int ExistingProfilesCount { get; set; }
}

public class ImportSectorsPreviewResult
{
    public int TotalInterfacesOnServer { get; set; }
    public int ImportableSectorsCount { get; set; }
    public int ExistingSectorsCount { get; set; }
    public int MissingIpCount { get; set; }
    public int InvalidInterfacesCount { get; set; }
    public bool IsRadioInterfaceCommandUnsupported { get; set; }
    public string? PreviewNote { get; set; }
}

public class ImportSectorsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int TotalOnServer { get; set; }
    public int AddedCount { get; set; }
    public int SkippedExisting { get; set; }
    public int SkippedMissingIp { get; set; }
    public List<string> Errors { get; set; } = new();
}
