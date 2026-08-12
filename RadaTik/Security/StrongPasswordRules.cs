using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;

namespace RadaTik.Security;

/// <summary>
/// معايير كلمة مرور قوية لمدير النظام والموظفين (كل الحسابات غير المرتبطة بـ <see cref="Models.ApplicationUser.ClientId"/>).
/// </summary>
public static class StrongPasswordRules
{
    public const int MinimumLength = 8;

    private static readonly string[] BlockedPasswords =
    [
        "admin@123",
        "123456",
        "password",
        "Password123!",
        "Admin@123456"
    ];

    public static void ConfigureIdentityOptions(PasswordOptions options)
    {
        options.RequiredLength = MinimumLength;
        options.RequireDigit = true;
        options.RequireLowercase = true;
        options.RequireUppercase = true;
        options.RequireNonAlphanumeric = true;
        options.RequiredUniqueChars = 4;
    }

    public static IEnumerable<string> Validate(string password, string? userName, string? email)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            yield return "كلمة المرور مطلوبة.";
            yield break;
        }

        if (password.Length < MinimumLength)
        {
            yield return $"كلمة المرور يجب ألا تقل عن {MinimumLength} حرفاً.";
        }

        if (!password.Any(char.IsDigit))
        {
            yield return "يجب أن تحتوي على رقم واحد على الأقل.";
        }

        if (!password.Any(char.IsLower))
        {
            yield return "يجب أن تحتوي على حرف صغير (a-z) واحد على الأقل.";
        }

        if (!password.Any(char.IsUpper))
        {
            yield return "يجب أن تحتوي على حرف كبير (A-Z) واحد على الأقل.";
        }

        if (!Regex.IsMatch(password, @"[^a-zA-Z0-9]"))
        {
            yield return "يجب أن تحتوي على رمز خاص واحد على الأقل (مثل ! @ # $).";
        }

        if (BlockedPasswords.Any(p => string.Equals(p, password, StringComparison.Ordinal)))
        {
            yield return "كلمة المرور ضعيفة أو مستخدمة كافتراضية. اختر كلمة مرور أقوى.";
        }

        if (!string.IsNullOrWhiteSpace(userName) &&
            password.Contains(userName, StringComparison.OrdinalIgnoreCase))
        {
            yield return "لا يمكن أن تحتوي كلمة المرور على اسم المستخدم.";
        }

        if (!string.IsNullOrWhiteSpace(email))
        {
            string localPart = email.Split('@')[0];
            if (localPart.Length >= 3 &&
                password.Contains(localPart, StringComparison.OrdinalIgnoreCase))
            {
                yield return "لا يمكن أن تحتوي كلمة المرور على جزء من البريد الإلكتروني.";
            }
        }
    }
}
