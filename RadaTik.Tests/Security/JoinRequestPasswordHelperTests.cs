using RadaTik.Models;
using RadaTik.Security;
using Xunit;

namespace RadaTik.Tests.Security;

public sealed class JoinRequestPasswordHelperTests
{
    [Fact]
    public void ShouldForcePasswordChangeOnLogin_WhenPasswordIsWeak_ReturnsTrue()
    {
        ApplicationUser user = new()
        {
            UserName = "manager1",
            Email = "manager@example.com"
        };

        bool result = JoinRequestPasswordHelper.ShouldForcePasswordChangeOnLogin(
            "short",
            user.UserName,
            user.Email,
            generatedTemporaryPassword: false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldForcePasswordChangeOnLogin_WhenPasswordIsStrong_ReturnsFalse()
    {
        ApplicationUser user = new()
        {
            UserName = "manager1",
            Email = "manager@example.com"
        };

        bool result = JoinRequestPasswordHelper.ShouldForcePasswordChangeOnLogin(
            "SecurePass123!",
            user.UserName,
            user.Email,
            generatedTemporaryPassword: false);

        Assert.False(result);
    }

    [Fact]
    public void ShouldForcePasswordChangeOnLogin_WhenTemporaryGenerated_ReturnsTrueEvenIfStrong()
    {
        bool result = JoinRequestPasswordHelper.ShouldForcePasswordChangeOnLogin(
            "SecurePass123!",
            "manager1",
            "manager@example.com",
            generatedTemporaryPassword: true);

        Assert.True(result);
    }

    [Fact]
    public void ApplyPostProvisionPasswordPolicy_SetsMustChangePasswordForWeakPassword()
    {
        ApplicationUser user = new()
        {
            UserName = "manager1",
            Email = "manager@example.com"
        };

        JoinRequestPasswordHelper.ApplyPostProvisionPasswordPolicy(user, "weak", generatedTemporaryPassword: false);

        Assert.True(user.MustChangePassword);
        Assert.Null(user.PasswordChangedAt);
    }

    [Fact]
    public void ApplyPostProvisionPasswordPolicy_ClearsMustChangePasswordForStrongPassword()
    {
        ApplicationUser user = new()
        {
            UserName = "manager1",
            Email = "manager@example.com"
        };

        JoinRequestPasswordHelper.ApplyPostProvisionPasswordPolicy(user, "SecurePass123!", generatedTemporaryPassword: false);

        Assert.False(user.MustChangePassword);
        Assert.NotNull(user.PasswordChangedAt);
    }
}
