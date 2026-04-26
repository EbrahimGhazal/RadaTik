namespace RadTik.Security;

/// <summary>
/// Centralized feature keys (Entitlements) used for paid/optional modules.
/// Values are persisted in DB (NetworkFeatures.Key), so avoid renaming keys.
/// </summary>
public static class FeatureKeys
{
    // Company Admin / Network modules
    public const string Networks = "Networks";
    public const string MikroTikServers = "MikroTikServers";
    public const string Profiles = "Profiles";
    public const string Users = "Users";
    public const string CollectionPoints = "CollectionPoints";

    // Shared modules (also used by employees/clients depending on roles)
    public const string Clients = "Clients";
    public const string Sectors = "Sectors";
    public const string Receivers = "Receivers";
    public const string Requests = "Requests";
    public const string PasswordResets = "PasswordResets";

    /// <summary>وحدة التقارير (اشتراك الشركة — يُفعّل تبويب التقارير).</summary>
    public const string Reports = "Reports";

    /// <summary>سعر خصم كل عملية توليد/تصدير تقرير (يُعرّفه مدير النظام فقط — لا يظهر كاشتراك للشركة).</summary>
    public const string ReportsExport = "ReportsExport";

    /// <summary>عمولة تحصيل من العملاء كنسبة من المبلغ (إعدادات النظام — لا تُعرض كاشتراك اختياري للشركة).</summary>
    public const string CollectionCommission = "CollectionCommission";

    /// <summary>عمولة فواتير الصيانة (ثابت أو نسبة) — إعداد نظام.</summary>
    public const string MaintenanceCommission = "MaintenanceCommission";

    /// <summary>أجور النقل الثابتة لفاتورة الصيانة — إعداد نظام.</summary>
    public const string MaintenanceTransportFee = "MaintenanceTransportFee";

    /// <summary>
    /// نسبة الضريبة المئوية المطبقة على سعر البروفايل (إعداد نظام مستقل، غير مرتبط برسوم إضافة السرعة/البروفايل).
    /// </summary>
    public const string ProfilePriceTax = "ProfilePriceTax";
}

