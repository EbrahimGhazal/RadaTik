using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class MikroTikApiSupportTests
{
    [Fact]
    public void IsEmptyResponse_DetectsTik4netEmptyType()
    {
        var ex = new InvalidOperationException("Response type '!empty' not supported");
        Assert.True(MikroTikApiSupport.IsEmptyResponse(ex));
    }

    [Fact]
    public void IsEmptyResponse_DetectsNestedEmptyType()
    {
        var inner = new InvalidOperationException("Response type '!empty' not supported");
        var outer = new InvalidOperationException("خطأ في إضافة المستخدم في المايكروتك", inner);
        Assert.True(MikroTikApiSupport.IsEmptyResponse(outer));
    }

    [Fact]
    public void IsEmptyResponse_DoesNotMatchAuthFailure()
    {
        var ex = new InvalidOperationException("invalid user name or password (6)");
        Assert.False(MikroTikApiSupport.IsEmptyResponse(ex));
    }

    [Fact]
    public void FindByName_WhenNameMissing_ReturnsNull()
    {
        Assert.Null(MikroTikApiSupport.FindByName(null!, "/ppp/secret/print", "user"));
    }

    [Theory]
    [InlineData("4Mbps", "4Mbps")]
    [InlineData("4Mbps", "4mbps")]
    [InlineData("4Mbps", "4 Mbps")]
    [InlineData("4 Mbps", "4Mbps")]
    [InlineData(" 4Mbps ", "4Mbps")]
    public void NamesMatch_IgnoresCaseAndWhitespace(string left, string right)
    {
        Assert.True(MikroTikApiSupport.NamesMatch(left, right));
    }

    [Theory]
    [InlineData("2Mbps", 2)]
    [InlineData("2 Mbps", 2)]
    [InlineData("2M", 2)]
    [InlineData("2M/2M", 2)]
    [InlineData("2048k", 2.048)]
    public void ParseSpeedMbps_ReadsCommonProfileNames(string text, double expected)
    {
        decimal? actual = MikroTikApiSupport.ParseSpeedMbps(text);
        Assert.NotNull(actual);
        Assert.Equal((decimal)expected, actual.Value, 3);
    }

    [Fact]
    public void ProfileIdentityMatch_MatchesBySpeedAlias()
    {
        Assert.True(MikroTikApiSupport.ProfileIdentityMatch("2Mbps", "2M"));
        Assert.True(MikroTikApiSupport.ProfileIdentityMatch("2Mbps", "default", "2M/2M"));
        Assert.False(MikroTikApiSupport.ProfileIdentityMatch("2Mbps", "8M"));
    }
}
