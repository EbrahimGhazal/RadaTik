using Microsoft.AspNetCore.Identity;



namespace RadaTik.Security;



/// <summary>

/// معايير كلمة مرور المشتركين (PPPoE / حساب العميل في النظام) — بدون تعقيد إداري.

/// </summary>

public static class ClientPasswordRules

{

    /// <summary>كلمة مرور دخول البوابة (Identity) — منفصلة عن PPPoE على MikroTik.</summary>
    public const int MinimumLength = 8;



    public static void ConfigureIdentityOptions(PasswordOptions options)

    {

        options.RequiredLength = MinimumLength;

        options.RequireDigit = false;

        options.RequireLowercase = false;

        options.RequireUppercase = false;

        options.RequireNonAlphanumeric = false;

        options.RequiredUniqueChars = 1;

    }



    public static IEnumerable<string> Validate(string? password)

    {

        if (string.IsNullOrWhiteSpace(password))

        {

            yield return "كلمة المرور مطلوبة.";

            yield break;

        }



        if (password.Length < MinimumLength)

        {

            yield return $"كلمة المرور يجب ألا تقل عن {MinimumLength} أحرف.";

        }

    }

}


