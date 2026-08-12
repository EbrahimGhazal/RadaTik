using Microsoft.AspNetCore.Identity;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services;

/// <summary>
/// تغيير كلمة مرور دخول بوابة المشترك (AspNetUsers) فقط — دون تعديل <see cref="Client.Password"/> أو MikroTik.
/// </summary>
public static class ClientPortalAccountPasswordService
{
    public static async Task<(bool Success, IReadOnlyList<string> Errors)> SetPortalPasswordAsync(
        UserManager<ApplicationUser> userManager,
        ApplicationUser user,
        string newPassword,
        bool completingRequiredChange,
        CancellationToken cancellationToken = default)
    {
        if (!user.ClientId.HasValue)
        {
            return (false, ["هذا الإجراء مخصص لحسابات المشتركين فقط."]);
        }

        foreach (string error in ClientPasswordRules.Validate(newPassword))
        {
            return (false, [error]);
        }

        string token = await userManager.GeneratePasswordResetTokenAsync(user);
        IdentityResult resetResult = await userManager.ResetPasswordAsync(user, token, newPassword);
        if (!resetResult.Succeeded)
        {
            return (false, resetResult.Errors.Select(e => e.Description).ToList());
        }

        if (completingRequiredChange)
        {
            user.MustChangePassword = false;
            user.PasswordChangedAt = DateTime.UtcNow;
            user.LastUpdated = DateTime.Now;
            IdentityResult updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return (false, updateResult.Errors.Select(e => e.Description).ToList());
            }
        }

        return (true, Array.Empty<string>());
    }
}
