using RadaTik.Services.Auth;
using Xunit;

namespace RadaTik.Tests.Services.Auth;

public class LoginIdentityResolverTests
{
    [Theory]
    [InlineData("0991234567", "0991234567", true)]
    [InlineData("0991-234-567", "0991234567", true)]
    [InlineData("+963 991 234 567", "0991234567", true)]
    [InlineData("963991234567", "0991234567", true)]
    [InlineData("0991234567", "0987654321", false)]
    [InlineData("", "0991234567", false)]
    public void PhonesMatch_ComparesNormalizedDigits(string left, string right, bool expected)
    {
        bool actual = LoginIdentityResolver.PhonesMatch(
            LoginIdentityResolver.DigitsOnly(left),
            LoginIdentityResolver.DigitsOnly(right));

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void PhoneSearchTokens_IncludesLocalAndInternationalVariants()
    {
        string[] tokens = LoginIdentityResolver.PhoneSearchTokens("0991234567");

        Assert.Contains("0991234567", tokens);
        Assert.Contains("991234567", tokens);
        Assert.Contains("963991234567", tokens);
    }
}
