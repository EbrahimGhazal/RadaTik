using Microsoft.AspNetCore.DataProtection;

namespace RadaTik.Security;

/// <summary>
/// تشفير الحقول الحساسة للتخزين في التطبيق/قاعدة البيانات فقط.
/// عند الإرسال إلى أنظمة خارجية (مثل MikroTik API) يجب استخدام <see cref="ToPlaintext"/>.
/// </summary>
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
            // Never return ciphertext to callers that may forward it to MikroTik.
            return null;
        }
    }

    /// <summary>
    /// يعيد كلمة المرور كنص صريح للاستخدام الخارجي (MikroTik وغيرها).
    /// لا يُرجع أبداً قيمة مشفّرة تبدأ بـ <c>enc::</c>.
    /// </summary>
    public static string? ToPlaintext(string? value)
    {
        string? plain = Unprotect(value);
        if (string.IsNullOrEmpty(plain))
        {
            return plain;
        }

        if (plain.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "تعذر فك تشفير كلمة المرور للاستخدام الخارجي. لن يتم إرسال قيمة مشفّرة إلى MikroTik.");
        }

        return plain;
    }
}
