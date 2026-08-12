using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using RadaTik.Security;

namespace RadaTik.Data.Infrastructure;

/// <summary>محولات EF لحقول حساسة (كلمات مرور MikroTik، إلخ).</summary>
public static class SensitiveDataConverters
{
    public static ValueConverter<string?, string?> NullableString { get; } = new(
        value => SensitiveDataProtector.Protect(value),
        value => SensitiveDataProtector.Unprotect(value));

    public static ValueConverter<string, string> String { get; } = new(
        value => SensitiveDataProtector.Protect(value) ?? string.Empty,
        value => SensitiveDataProtector.Unprotect(value) ?? string.Empty);
}
