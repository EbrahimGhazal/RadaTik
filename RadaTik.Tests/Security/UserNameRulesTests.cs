using RadaTik.Security;
using Xunit;

namespace RadaTik.Tests.Security;

public class UserNameRulesTests
{
    [Theory]
    [InlineData("company_admin")]
    [InlineData("Admin.Net")]
    [InlineData("user-01")]
    [InlineData("SkyBeam")]
    public void IsValid_AcceptsEnglishUserNames(string userName)
    {
        Assert.True(UserNameRules.IsValid(userName));
        Assert.Null(UserNameRules.ValidateOrError(userName));
    }

    [Theory]
    [InlineData("مدير الشركة")]
    [InlineData("admin user")]
    [InlineData("admin\tuser")]
    [InlineData("user عربي")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValid_RejectsArabicSpacesOrEmpty(string userName)
    {
        Assert.False(UserNameRules.IsValid(userName));
        Assert.NotNull(UserNameRules.ValidateOrError(userName));
    }
}
