namespace RadTik.Security;

/// <summary>
/// Feature definitions shown in the CompanyAdmin "subscription/features" UI.
/// These are product-level definitions (names/descriptions), not the enabled state.
/// </summary>
public static class FeatureCatalog
{
    public sealed record FeatureDefinition(
        string Key,
        string DisplayName,
        string Category,
        string Description,
        bool DefaultEnabled = true);

    /// <summary>
    /// The canonical list of features that can be enabled/disabled per company (network).
    /// </summary>
    public static readonly IReadOnlyList<FeatureDefinition> All = new List<FeatureDefinition>
    {
        new(FeatureKeys.Networks, "إدارة الشبكة", "الإدارة", "إدارة بيانات الشركة/الشبكة، الفروع، والإعدادات الأساسية.", DefaultEnabled: false),
        new(FeatureKeys.Users, "إدارة الموظفين/المستخدمين", "الإدارة", "إدارة الموظفين/المستخدمين/نقاط البيع/المشتركين وربط صلاحياتهم.", DefaultEnabled: false),
        new(FeatureKeys.CollectionPoints, "نقاط التحصيل", "الإدارة", "إدارة حسابات نقاط التحصيل والتحويلات.", DefaultEnabled: false),

        new(FeatureKeys.MikroTikServers, "خوادم MikroTik", "النظام", "إضافة/تعديل خوادم MikroTik ومزامنة البيانات.", DefaultEnabled: false),
        new(FeatureKeys.Profiles, "باقات السرعة", "النظام", "إدارة الباقات والأسعار والمزامنة مع MikroTik.", DefaultEnabled: false),

        new(FeatureKeys.Clients, "إدارة المشتركين/العملاء", "المشتركين", "إضافة/تعديل المشتركين/العملاء وتجديد الاشتراكات.", DefaultEnabled: false),
        new(FeatureKeys.Sectors, "المرسلات (القطاعات)", "الأجهزة", "إدارة القطاعات وإعداداتها.", DefaultEnabled: false),
        new(FeatureKeys.Receivers, "المستقبلات", "الأجهزة", "إدارة المستقبلات وربطها بالقطاعات.", DefaultEnabled: false),

        new(FeatureKeys.Requests, "إدارة الطلبات", "الدعم", "طلبات الصيانة وتغيير السرعة (حسب الصلاحيات).", DefaultEnabled: false),
        new(FeatureKeys.PasswordResets, "طلبات استعادة كلمة المرور", "الأمان", "إدارة طلبات استعادة كلمة المرور والحسابات.", DefaultEnabled: false),

        new(FeatureKeys.Reports, "التقارير", "التقارير", "تقارير المشتركين والمرسلات والمستقبلات والخوادم ونقاط التحصيل مع تصدير وطباعة.", DefaultEnabled: false),
    };
}

