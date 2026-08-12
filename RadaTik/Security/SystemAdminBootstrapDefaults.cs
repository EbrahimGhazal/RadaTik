namespace RadaTik.Security;

/// <summary>
/// بيانات حساب مدير النظام الافتراضي عند إنشاء قاعدة البيانات لأول مرة.
/// </summary>
public static class SystemAdminBootstrapDefaults
{
    public const string UserName = "admin";
    public const string Password = "admin@123";
    public const string Email = "admin@radatik.local";
    public const string FullName = "مدير النظام";
}
