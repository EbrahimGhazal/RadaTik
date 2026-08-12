using System.Text.RegularExpressions;
using RadaTik.Domain.Common;

namespace RadaTik.Domain.ValueObjects;

/// <summary>اسم مستخدم PPPoE (حروف وأرقام و ._@- حتى 64 حرفاً).</summary>
public readonly record struct PppoeUsername
{
    private static readonly Regex ValidPattern = new(@"^[a-zA-Z0-9._@-]{1,64}$", RegexOptions.Compiled);

    public string Value { get; }

    private PppoeUsername(string value) => Value = value;

    public static ServiceResult<PppoeUsername> TryCreate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return ServiceResult<PppoeUsername>.Fail("اسم المستخدم مطلوب");
        }

        string trimmed = raw.Trim();
        if (!ValidPattern.IsMatch(trimmed))
        {
            return ServiceResult<PppoeUsername>.Fail(
                "اسم المستخدم يجب أن يتكون من حروف وأرقام (يمكن استخدام . _ @ -) ولا يتجاوز 64 حرفاً");
        }

        return ServiceResult<PppoeUsername>.Ok(new PppoeUsername(trimmed));
    }
}
