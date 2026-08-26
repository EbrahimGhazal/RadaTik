using System.Net.Sockets;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class MikroTikConnectionSupportTests
{
    [Fact]
    public void IsHardConnectFailure_DetectsSocketTimeout()
    {
        Exception ex = new InvalidOperationException(
            "wrap",
            new SocketException((int)SocketError.TimedOut));

        Assert.True(MikroTikConnectionSupport.IsHardConnectFailure(ex));
    }

    [Fact]
    public void IsHardConnectFailure_IgnoresUnrelatedErrors()
    {
        Exception ex = new InvalidOperationException("profile missing on device");
        Assert.False(MikroTikConnectionSupport.IsHardConnectFailure(ex));
    }
}
