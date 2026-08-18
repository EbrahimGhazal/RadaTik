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
}
