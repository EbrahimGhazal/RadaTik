using RadaTik.Services.SectorRadio;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class RadioStationMatcherTests
{
    [Fact]
    public void Pick_PrefersMacOverIp()
    {
        RadioStationSignal[] stations =
        [
            new() { MacAddress = "AA:BB:CC:DD:EE:01", LastIp = "10.0.0.5", SignalDbm = -70 },
            new() { MacAddress = "AA:BB:CC:DD:EE:02", LastIp = "10.0.0.8", SignalDbm = -55 }
        ];

        RadioStationMatch? match = RadioStationMatcher.Pick(stations, "10.0.0.5", "aa-bb-cc-dd-ee-02", "wlan1");

        Assert.NotNull(match);
        Assert.Equal("mac", match.Reason);
        Assert.Equal(-55, match.Station.SignalDbm);
    }

    [Fact]
    public void Pick_MatchesReceiverIpWhenMacMissing()
    {
        RadioStationSignal[] stations =
        [
            new() { MacAddress = "11:22:33:44:55:66", LastIp = "192.168.10.20", SignalDbm = -62 }
        ];

        RadioStationMatch? match = RadioStationMatcher.Pick(stations, "192.168.10.20", null, null);

        Assert.NotNull(match);
        Assert.Equal("ip", match.Reason);
    }

    [Fact]
    public void Pick_SingleStationOnInterface_IsUsed()
    {
        RadioStationSignal[] stations =
        [
            new() { MacAddress = "11:22:33:44:55:66", InterfaceName = "wlan1", SignalDbm = -60 }
        ];

        RadioStationMatch? match = RadioStationMatcher.Pick(stations, null, null, "wlan1");

        Assert.NotNull(match);
        Assert.Equal("single", match.Reason);
    }
}
