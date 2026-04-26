namespace RadTik.Services.MikroTikSync;

/// <summary>
/// يمثل مهمة مزامنة مع MikroTik بعد تغيير في قاعدة البيانات
/// </summary>
public sealed class MikroTikSyncJob
{
    /// <summary>نوع الكيان: Client أو Profile</summary>
    public string EntityType { get; set; } = null!;

    /// <summary>معرف الكيان في قاعدة البيانات</summary>
    public int EntityId { get; set; }

    /// <summary>الإجراء: Add, Update, Delete</summary>
    public MikroTikSyncAction Action { get; set; }

    /// <summary>معرف خادم MikroTik (للعميل أو البروفايل)</summary>
    public int? ServerId { get; set; }

    /// <summary>اسم المستخدم PPPoE (للعميل - يُستخدم في Delete)</summary>
    public string? UserName { get; set; }

    /// <summary>اسم البروفايل (للبروفايل - يُستخدم في Delete)</summary>
    public string? ProfileName { get; set; }
}

/// <summary>
/// نوع إجراء المزامنة
/// </summary>
public enum MikroTikSyncAction
{
    Add,
    Update,
    Delete
}
