using Microsoft.AspNetCore.Identity;

using RadaTik.Models;



namespace RadaTik.Security;



/// <summary>

/// يفرض كلمة مرور قوية على الحسابات غير المرتبطة بمشترك (مدراء، موظفون، إلخ).

/// </summary>

public sealed class StrongPasswordValidator : IPasswordValidator<ApplicationUser>

{

    public Task<IdentityResult> ValidateAsync(

        UserManager<ApplicationUser> manager,

        ApplicationUser user,

        string? password)

    {

        if (user.ClientId.HasValue)

        {

            return Task.FromResult(IdentityResult.Success);

        }

        if (BootstrapPasswordValidationScope.IsActive)

        {

            return Task.FromResult(IdentityResult.Success);

        }



        List<string> errors = StrongPasswordRules.Validate(password ?? string.Empty, user.UserName, user.Email)

            .ToList();



        return Task.FromResult(errors.Count == 0

            ? IdentityResult.Success

            : IdentityResult.Failed(errors.Select(e => new IdentityError { Description = e }).ToArray()));

    }

}


