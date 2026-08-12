using RadaTik.Domain.Common;

namespace RadaTik.Domain.ValueObjects;

/// <summary>رقم هاتف مشترك مُطبّع (حتى 15 حرفاً).</summary>
public readonly record struct PhoneNumber
{
    public string Value { get; }

    private PhoneNumber(string value) => Value = value;

    public static ServiceResult<PhoneNumber> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ServiceResult<PhoneNumber>.Ok(new PhoneNumber("0"));
        }

        string cleaned = new string(raw.Where(ch => char.IsDigit(ch) || ch == '+' || ch == '-' || ch == ' ').ToArray());
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            return ServiceResult<PhoneNumber>.Ok(new PhoneNumber("0"));
        }

        string normalized = cleaned.Length > 15 ? cleaned[..15] : cleaned;
        return ServiceResult<PhoneNumber>.Ok(new PhoneNumber(normalized));
    }
}
