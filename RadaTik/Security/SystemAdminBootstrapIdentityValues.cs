using Microsoft.AspNetCore.Identity;
using RadaTik.Models;

namespace RadaTik.Security;

public sealed record SystemAdminBootstrapIdentityValues(
    string PasswordHash,
    string SecurityStamp,
    string ConcurrencyStamp)
{
    public static SystemAdminBootstrapIdentityValues Create()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        return new SystemAdminBootstrapIdentityValues(
            hasher.HashPassword(null!, SystemAdminBootstrapDefaults.Password),
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString());
    }
}
