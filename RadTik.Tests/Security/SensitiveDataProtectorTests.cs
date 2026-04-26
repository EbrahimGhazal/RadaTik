using RadTik.Security;
using Xunit;

namespace RadTik.Tests.Security;

public class SensitiveDataProtectorTests
{
    [Fact]
    public void ProtectAndUnprotect_RoundTripsPlaintext()
    {
        const string plain = "my-secret-password";

        var encrypted = SensitiveDataProtector.Protect(plain);
        var decrypted = SensitiveDataProtector.Unprotect(encrypted);

        Assert.NotNull(encrypted);
        Assert.NotEqual(plain, encrypted);
        Assert.Equal(plain, decrypted);
    }

    [Fact]
    public void Unprotect_PlaintextLegacyValue_ReturnsOriginalValue()
    {
        const string legacyValue = "legacy-plain-text";
        var output = SensitiveDataProtector.Unprotect(legacyValue);
        Assert.Equal(legacyValue, output);
    }
}
