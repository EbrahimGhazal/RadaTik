using Microsoft.AspNetCore.Identity;
using RadaTik.Models;

namespace RadaTik.Security;

/// <summary>
/// إنشاء حسابات طلبات الانضمام: قبول كلمة المرور المطلوبة عند الموافقة،
/// مع إجبار تغييرها لاحقاً إذا لم تستوفِ معايير القوة.
/// </summary>
public static class JoinRequestPasswordHelper
{
    public static bool IsStrongPassword(string password, string? userName, string? email) =>
        !StrongPasswordRules.Validate(password, userName, email).Any();

    public static bool ShouldForcePasswordChangeOnLogin(
        string password,
        string? userName,
        string? email,
        bool generatedTemporaryPassword) =>
        generatedTemporaryPassword || !IsStrongPassword(password, userName, email);

    public static async Task<IdentityResult> CreateUserAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string password)
    {
        using (BootstrapPasswordValidationScope.Enter())
        {
            return await userManager.CreateAsync(user, password);
        }
    }

    public static async Task<IdentityResult> ResetPasswordAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string password)
    {
        using (BootstrapPasswordValidationScope.Enter())
        {
            string token = await userManager.GeneratePasswordResetTokenAsync(user);
            return await userManager.ResetPasswordAsync(user, token, password);
        }
    }

    public static void ApplyPostProvisionPasswordPolicy(
        ApplicationUser user,
        string password,
        bool generatedTemporaryPassword)
    {
        bool mustChange = ShouldForcePasswordChangeOnLogin(
            password,
            user.UserName,
            user.Email,
            generatedTemporaryPassword);

        user.MustChangePassword = mustChange;
        user.PasswordChangedAt = mustChange ? null : DateTime.UtcNow;
    }
}
