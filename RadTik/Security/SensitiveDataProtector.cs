using Microsoft.AspNetCore.DataProtection;

namespace RadTik.Security;

public static class SensitiveDataProtector
{
    private const string Prefix = "enc::";

    private static readonly Lazy<IDataProtector> Protector = new(() =>
        DataProtectionProvider.Create("RadTik.SensitiveFields")
            .CreateProtector("RadTik.Security.SensitiveDataProtector.v1"));

    public static string? Protect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            return value;
        }

        return Prefix + Protector.Value.Protect(value);
    }

    public static string? Unprotect(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        if (!value.StartsWith(Prefix, StringComparison.Ordinal))
        {
            // Backward compatibility for historical plaintext rows.
            return value;
        }

        var cipherText = value[Prefix.Length..];
        try
        {
            return Protector.Value.Unprotect(cipherText);
        }
        catch
        {
            // Keep application alive even if legacy/corrupt payloads exist.
            return value;
        }
    }
}
