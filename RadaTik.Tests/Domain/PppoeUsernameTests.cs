using RadaTik.Domain.ValueObjects;
using Xunit;

namespace RadaTik.Tests.Domain;

public sealed class PppoeUsernameTests
{
    [Fact]
    public void TryCreate_ValidUsername_ReturnsOk()
    {
        var result = PppoeUsername.TryCreate("user_01@test");
        Assert.True(result.IsSuccess);
        Assert.Equal("user_01@test", result.Value!.Value);
    }

    [Fact]
    public void TryCreate_InvalidChars_ReturnsFail()
    {
        var result = PppoeUsername.TryCreate("user name");
        Assert.False(result.IsSuccess);
    }
}
