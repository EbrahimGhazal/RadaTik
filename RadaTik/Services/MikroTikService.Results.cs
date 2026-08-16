namespace RadaTik.Services;

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
    /// <summary>عملاء موجودون تم تحديث بياناتهم من MikroTik أثناء إعادة المزامنة.</summary>
    public int UpdatedCount { get; set; }
    public int ExistingCount { get; set; }
    public int DuplicateCount { get; set; }
    /// <summary>مشتركون كانوا في الشبكة بدون MikroTikServerId وتم ربطهم بالسيرفر.</summary>
    public int RelinkedCount { get; set; }
    /// <summary>بروفايلات أُنشئت تلقائياً لأن اسم البروفايل موجود على MikroTik فقط.</summary>
    public int ProfilesCreatedCount { get; set; }
    public int FailedCount { get; set; }
    public int UsersCreatedCount { get; set; }
    public int UsersFailedCount { get; set; }
    public List<string> Errors { get; set; } = new();
}

public class ImportUsersPreviewResult
{
    public int TotalUsersOnServer { get; set; }
    public int ImportableUsersCount { get; set; }
    /// <summary>عملاء موجودون اختلفت بياناتهم القابلة للمزامنة عن بيانات MikroTik.</summary>
    public int UpdatableUsersCount { get; set; }
    public int ExistingUsersCount { get; set; }
    /// <summary>من القابلين للاستيراد: موجودون بنفس الاسم على سيرفر آخر في الشبكة.</summary>
    public int DuplicateUsersCount { get; set; }
    /// <summary>موجودون في الشبكة بلا سيرفر — يُربطون عند الاستيراد.</summary>
    public int RelinkableUsersCount { get; set; }
    /// <summary>بروفايلهم غير موجود في DB (سيُنشأ تلقائياً إن وُجد اسم البروفايل على MikroTik).</summary>
    public int MissingProfileCount { get; set; }
    public int InvalidUsersCount { get; set; }
    public bool HasConnectionError { get; set; }
    public string? PreviewNote { get; set; }
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
