using Microsoft.AspNetCore.DataProtection;

namespace RadaTik.Security;

/// <summary>
/// مجلد مفاتيح تشفير الحقول الحساسة (كلمات مرور MikroTik وغيرها).
/// يجب أن يبقى خارج الحاوية حتى لا تُفقد المفاتيح عند كل نشر.
/// </summary>
public static class RadaTikDataProtection
{
    public const string ApplicationName = "RadaTik.SensitiveFields";
    public const string SensitivePurpose = "RadaTik.Security.SensitiveDataProtector.v1";
    public const string KeysPathEnvironmentVariable = "RADATIK_DATA_PROTECTION_KEYS_PATH";

    public static string ResolveKeysDirectory()
    {
        string? configured = Environment.GetEnvironmentVariable(KeysPathEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }

        if (OperatingSystem.IsWindows())
        {
            string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                localAppData = Path.GetTempPath();
            }

            return Path.Combine(localAppData, "ASP.NET", "DataProtection-Keys");
        }

        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            home = Environment.GetEnvironmentVariable("HOME") ?? "/root";
        }

        return Path.Combine(home, ".aspnet", "DataProtection-Keys");
    }

    public static DirectoryInfo EnsureKeysDirectory()
    {
        return Directory.CreateDirectory(ResolveKeysDirectory());
    }

    public static IDataProtectionProvider CreateProvider(string? keysDirectory = null)
    {
        DirectoryInfo directory = string.IsNullOrWhiteSpace(keysDirectory)
            ? EnsureKeysDirectory()
            : Directory.CreateDirectory(keysDirectory);
        return DataProtectionProvider.Create(directory, builder =>
        {
            builder.SetApplicationName(ApplicationName);
        });
    }
}
