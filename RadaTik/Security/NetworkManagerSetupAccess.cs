namespace RadaTik.Security;

/// <summary>
/// مسارات مسموحة لمدير الشركة أثناء إجبار تغيير كلمة المرور أو تهيئة الشبكة/المحفظة.
/// </summary>
public static class NetworkManagerSetupAccess
{
    public static bool IsAccountPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase);

    public static bool IsRequiredPasswordPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith("/networkManager/setup/requiredPassword", StringComparison.OrdinalIgnoreCase);

    public static bool IsWalletPath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith("/networkManager/wallet", StringComparison.OrdinalIgnoreCase);

    public static bool IsNetworkCreatePath(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith("/networkManager/Network/Create", StringComparison.OrdinalIgnoreCase);

    public static bool IsAllowedDuringMandatoryPasswordChange(string path) =>
        IsAccountPath(path)
        || IsRequiredPasswordPath(path)
        || IsWalletPath(path)
        || IsNetworkCreatePath(path);

    /// <summary>قبل وجود شبكة رئيسية: إنشاء الشبكة + الحساب فقط (لا محفظة بعد).</summary>
    public static bool IsAllowedBeforeMainNetwork(string path) =>
        IsAccountPath(path)
        || IsRequiredPasswordPath(path)
        || IsNetworkCreatePath(path);

    /// <summary>أثناء باب تمويل المحفظة الإلزامي.</summary>
    public static bool IsAllowedDuringMandatoryWalletFunding(string path) =>
        IsAccountPath(path)
        || IsRequiredPasswordPath(path)
        || IsWalletPath(path);
}
