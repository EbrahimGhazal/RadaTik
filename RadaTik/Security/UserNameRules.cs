using System.Text.RegularExpressions;

namespace RadaTik.Security;

/// <summary>
/// قواعد اسم المستخدم لحسابات الدخول (طلبات الانضمام وتسجيل الأدوار).
/// إنجليزي فقط، بلا فراغات، ولا يقبل العربية أو رموزاً غير مسموحة.
/// </summary>
public static partial class UserNameRules
{
    public const string AllowedPattern = "^[A-Za-z0-9._-]+$";
    public const string AllowedHint =
        "اسم المستخدم باللغة الإنكليزية فقط، بدون فراغات، ولا يُقبل إدخال العربية.";

    public const string InvalidMessage =
        "اسم المستخدم يجب أن يكون باللغة الإنكليزية فقط وبدون فراغات (أحرف وأرقام و . _ -). العربية غير مقبولة.";

    [GeneratedRegex(AllowedPattern, RegexOptions.CultureInvariant)]
    private static partial Regex AllowedRegex();

    public static bool IsValid(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return false;
        }

        string value = userName.Trim();
        if (value.Contains(' ', StringComparison.Ordinal) ||
            value.Any(char.IsWhiteSpace))
        {
            return false;
        }

        return AllowedRegex().IsMatch(value);
    }

    public static string? ValidateOrError(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
        {
            return "اسم المستخدم مطلوب.";
        }

        return IsValid(userName) ? null : InvalidMessage;
    }
}
