using System.Text.RegularExpressions;
using RadaTik.Domain.Common;

namespace RadaTik.Domain.ValueObjects;

/// <summary>رقم مشترك (SID) رقمي حتى 20 خانة.</summary>
public readonly record struct SubscriberSid
{
    private static readonly Regex NumericSid = new(@"^\d+$", RegexOptions.Compiled);

    public string Value { get; }

    private SubscriberSid(string value) => Value = value;

    public static ServiceResult<SubscriberSid> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ServiceResult<SubscriberSid>.Fail("رقم المشترك (SID) مطلوب.");
        }

        string trimmed = raw.Trim();
        if (!NumericSid.IsMatch(trimmed) || trimmed.Length > 20)
        {
            return ServiceResult<SubscriberSid>.Fail("رقم المشترك يجب أن يكون أرقاماً فقط (حتى 20 خانة).");
        }

        return ServiceResult<SubscriberSid>.Ok(new SubscriberSid(trimmed));
    }

    public static string GenerateNew() => DateTime.Now.Ticks.ToString()[^10..];
}
