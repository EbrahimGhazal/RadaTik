using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class SocialMediaCatalogTests
{
    [Fact]
    public void TryNormalizeHttpUrl_AcceptsHttpsAndRejectsLocalhost()
    {
        Assert.True(SocialMediaCatalog.TryNormalizeHttpUrl("facebook.com/radatik", out string url, out _));
        Assert.StartsWith("https://facebook.com/", url);
        Assert.False(SocialMediaCatalog.TryNormalizeHttpUrl("http://localhost/x", out _, out string? error));
        Assert.Contains("محلي", error);
    }

    [Fact]
    public void NormalizeSocialUrl_BuildsWhatsAppAndTelegramLinks()
    {
        Assert.Equal("https://wa.me/963991234567", SocialMediaCatalog.NormalizeSocialUrl(SocialMediaPlatform.WhatsApp, "+963 991 234 567"));
        Assert.Equal("https://t.me/radatik", SocialMediaCatalog.NormalizeSocialUrl(SocialMediaPlatform.Telegram, "@radatik"));
    }

    [Fact]
    public void TryNormalizePhone_RequiresReasonableDigitCount()
    {
        Assert.True(SocialMediaCatalog.TryNormalizePhone("0991 234 567", out string phone, out _));
        Assert.Equal("0991234567", phone);
        Assert.False(SocialMediaCatalog.TryNormalizePhone("123", out _, out _));
        Assert.Equal("tel:+963991", SocialMediaCatalog.TelHref("+963991"));
    }
}
