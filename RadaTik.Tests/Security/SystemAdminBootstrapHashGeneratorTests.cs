using Microsoft.AspNetCore.Identity;
using RadaTik.Models;
using RadaTik.Security;
using Xunit;
using Xunit.Abstractions;

namespace RadaTik.Tests.Security;

/// <summary>يُستخدم لتوليد قيم Identity لبذور SQL (تشغيل اختياري).</summary>
public class SystemAdminBootstrapHashGeneratorTests(ITestOutputHelper output)
{
    [Fact(Skip = "توليد يدوي فقط — أزل Skip لتوليد PasswordHash")]
    public void Print_bootstrap_identity_values()
    {
        var hasher = new PasswordHasher<ApplicationUser>();
        string hash = hasher.HashPassword(null!, SystemAdminBootstrapDefaults.Password);
        output.WriteLine($"PasswordHash={hash}");
        output.WriteLine($"SecurityStamp={Guid.NewGuid()}");
        output.WriteLine($"ConcurrencyStamp={Guid.NewGuid()}");
    }
}
