using RadaTik.Domain.ValueObjects;
using Xunit;

namespace RadaTik.Tests.Domain;

public sealed class PhoneNumberTests
{
    [Fact]
    public void TryCreate_NormalizesAndTruncates()
    {
        var result = PhoneNumber.TryCreate("  0999-888-7777-extra  ");
        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Value.Length <= 15);
    }

    [Fact]
    public void TryCreate_Empty_ReturnsZero()
    {
        var result = PhoneNumber.TryCreate("");
        Assert.True(result.IsSuccess);
        Assert.Equal("0", result.Value!.Value);
    }
}
