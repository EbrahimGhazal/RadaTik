using RadaTik.Domain.ValueObjects;
using Xunit;

namespace RadaTik.Tests.Domain;

public sealed class SubscriberSidTests
{
    [Fact]
    public void TryCreate_ValidNumeric_ReturnsOk()
    {
        var result = SubscriberSid.TryCreate("12345");
        Assert.True(result.IsSuccess);
        Assert.Equal("12345", result.Value!.Value);
    }

    [Fact]
    public void TryCreate_Invalid_ReturnsFail()
    {
        var result = SubscriberSid.TryCreate("abc");
        Assert.False(result.IsSuccess);
    }
}
