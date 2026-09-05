using Microsoft.AspNetCore.DataProtection;
using RadaTik.Security;
using Xunit;

namespace RadaTik.Tests.Security;

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

    [Fact]
    public void ToPlaintext_EncryptedValue_ReturnsPlaintextWithoutCipherPrefix()
    {
        const string plain = "pppoe-secret";
        string? encrypted = SensitiveDataProtector.Protect(plain);
        string? forMikroTik = SensitiveDataProtector.ToPlaintext(encrypted);

        Assert.NotNull(encrypted);
        Assert.StartsWith("enc::", encrypted);
        Assert.Equal(plain, forMikroTik);
        Assert.DoesNotContain("enc::", forMikroTik);
    }

    [Fact]
    public void ToPlaintext_AlreadyPlaintext_ReturnsSameValue()
    {
        const string plain = "already-plain";
        Assert.Equal(plain, SensitiveDataProtector.ToPlaintext(plain));
    }

    [Fact]
    public void SharedKeyDirectory_AllowsNewProviderToDecryptExistingCipher()
    {
        string keysDir = Path.Combine(Path.GetTempPath(), "radatik-dp-" + Guid.NewGuid().ToString("N"));
        try
        {
            IDataProtector first = RadaTikDataProtection.CreateProvider(keysDir)
                .CreateProtector(RadaTikDataProtection.SensitivePurpose);
            string cipher = first.Protect("router-secret");

            IDataProtector second = RadaTikDataProtection.CreateProvider(keysDir)
                .CreateProtector(RadaTikDataProtection.SensitivePurpose);

            Assert.Equal("router-secret", second.Unprotect(cipher));
            Assert.True(Directory.EnumerateFiles(keysDir, "*.xml").Any());
        }
        finally
        {
            if (Directory.Exists(keysDir))
            {
                Directory.Delete(keysDir, recursive: true);
            }
        }
    }
}

public sealed class DataProtectionComposeTests
{
    [Fact]
    public void DockerCompose_PersistsDataProtectionKeysOutsideContainer()
    {
        string compose = File.ReadAllText(FindRepoFile("docker-compose.yml"));
        Assert.Contains("RADATIK_DATA_PROTECTION_KEYS_PATH=/var/radatik/dp-keys", compose);
        Assert.Contains("/opt/radatik/dp-keys:/var/radatik/dp-keys", compose);
    }

    private static string FindRepoFile(string relativePath)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(relativePath);
    }
}
