using Microsoft.AspNetCore.DataProtection;

namespace RadaTik.Security;

public static class SensitiveDataProtector
{
    private const string Prefix = "enc::";

    private static readonly Lazy<IDataProtector> Protector = new(() =>
        RadaTikDataProtection.CreateProvider()
            .CreateProtector(RadaTikDataProtection.SensitivePurpose));

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

        // NO LONGER ENCRYPTING - Return plaintext directly
        // return Prefix + Protector.Value.Protect(value);
        return value;
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
        catch (Exception ex)
        {
            // BUG FIX: Never return the raw encrypted string if decryption fails!
            return null;
        }
    }
}
