using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class MikroTikPingParserTests
{
    [Fact]
    public void TimeValue_MeansReachable()
    {
        bool reachable = MikroTikPingParser.IsReachable(
        [
            ("0", "12ms", "", "0")
        ]);

        Assert.True(reachable);
    }

    [Fact]
    public void ReceivedCount_MeansReachable()
    {
        bool reachable = MikroTikPingParser.IsReachable(
        [
            ("1", "", "", "0")
        ]);

        Assert.True(reachable);
    }

    [Fact]
    public void TimeoutStatus_MeansUnreachable()
    {
        bool reachable = MikroTikPingParser.IsReachable(
        [
            ("0", "", "timeout", "100")
        ]);

        Assert.False(reachable);
    }

    [Fact]
    public void EmptyRows_MeansUnreachable()
    {
        bool reachable = MikroTikPingParser.IsReachable([]);

        Assert.False(reachable);
    }
}
