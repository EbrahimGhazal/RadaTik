using RadaTik.Services;
using RadaTik.Services.Calibration;
using RadaTik.Services.SectorRadio;
using RadaTik.ViewModels;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class AntennaCalibrationSessionStoreTests
{
    [Fact]
    public void ApplyRadio_HoldsPeakAndMatchesByIp()
    {
        AntennaCalibrationSessionStore store = new();
        AntennaCalibrationSession session = store.Create(Sample("AA11BB", "10.1.2.3"));
        SectorRadioStationsResult radio = new()
        {
            Success = true,
            StatusMessage = "ok",
            InterfaceName = "wlan1",
            NoiseFloorDbm = -95,
            FrequencyMhz = 5800,
            Stations =
            [
                new RadioStationSignal
                {
                    MacAddress = "AA:BB:CC:DD:EE:FF",
                    LastIp = "10.1.2.3",
                    InterfaceName = "wlan1",
                    SignalDbm = -62,
                    SnrDb = 33,
                    CcqPercent = 90
                }
            ]
        };

        Assert.True(store.TryApplyRadio(session.Code, radio, DateTime.UtcNow));
        AntennaCalibrationSnapshot first = store.ToSnapshot(store.Get(session.Code)!);
        Assert.True(first.Radio.Available);
        Assert.Equal(-62, first.Radio.SignalDbm);
        Assert.Equal(-62, first.Radio.PeakSignalDbm);
        Assert.Equal("ip", first.Radio.MatchReason);

        SectorRadioStationsResult weaker = new()
        {
            Success = true,
            StatusMessage = "ok",
            InterfaceName = "wlan1",
            NoiseFloorDbm = -95,
            FrequencyMhz = 5800,
            Stations =
            [
                new RadioStationSignal
                {
                    MacAddress = "AA:BB:CC:DD:EE:FF",
                    LastIp = "10.1.2.3",
                    InterfaceName = "wlan1",
                    SignalDbm = -70,
                    SnrDb = 25,
                    CcqPercent = 70
                }
            ]
        };
        store.TryApplyRadio(session.Code, weaker, DateTime.UtcNow);
        AntennaCalibrationSnapshot second = store.ToSnapshot(store.Get(session.Code)!);
        Assert.Equal(-70, second.Radio.SignalDbm);
        Assert.Equal(-62, second.Radio.PeakSignalDbm);

        Assert.True(store.TryResetPeak(session.Code));
        AntennaCalibrationSnapshot reset = store.ToSnapshot(store.Get(session.Code)!);
        Assert.Equal(-70, reset.Radio.PeakSignalDbm);
    }

    [Fact]
    public void Snapshot_AllowsMapPointWithoutSavedReceiver()
    {
        AntennaCalibrationSessionStore store = new();
        AntennaCalibrationSession session = Sample("MAP001", null);
        session = store.Create(session);
        AntennaCalibrationSnapshot snap = store.ToSnapshot(session);
        Assert.False(snap.SavedReceiver);
        Assert.Contains("افتح شاشة الميدان", snap.Advice, StringComparison.Ordinal);
    }

    [Fact]
    public void PathSnapshot_WarnsWhenTerrainBlocks()
    {
        AntennaCalibrationPathSnapshot path = AntennaCalibrationPathSnapshot.From(new LineOfSightResult
        {
            Success = true,
            PathClear = false,
            TerrainClear = false,
            FresnelClear = false,
            FrequencyMhzUsed = 5800
        });
        Assert.True(path.Analyzed);
        Assert.Contains("التضاريس", path.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Advice_PrefersTerrainBlockOverGeometry()
    {
        AntennaCalibrationSession session = Sample("BLK001", null);
        session = new AntennaCalibrationSession
        {
            Code = session.Code,
            NetworkId = session.NetworkId,
            SectorId = session.SectorId,
            SectorName = session.SectorName,
            ReceiverName = session.ReceiverName,
            ReceiverLatitude = session.ReceiverLatitude,
            ReceiverLongitude = session.ReceiverLongitude,
            Alignment = session.Alignment,
            SectorAntennaMsl = session.SectorAntennaMsl,
            ReceiverAntennaMsl = session.ReceiverAntennaMsl,
            Path = AntennaCalibrationPathSnapshot.From(new LineOfSightResult
            {
                Success = true,
                PathClear = false,
                TerrainClear = false,
                FresnelClear = false
            })
        };
        AntennaCalibrationLiveSnapshot live = new()
        {
            Connected = true,
            PoseValid = true,
            HorizontalAligned = true,
            VerticalAligned = true,
            CompassUnstable = false
        };
        string advice = AntennaCalibrationAdvice.Build(
            session,
            live,
            live,
            geometryLocked: true,
            radio: session.Radio.ToSnapshot());
        Assert.Contains("التضاريس", advice, StringComparison.Ordinal);
    }

    private static AntennaCalibrationSession Sample(string code, string? ip)
    {
        AntennaAlignmentResult alignment = LineOfSightMath.ComputeAlignment(
            33.50, 36.30, 820, 90, 90,
            33.53, 36.33, 790);
        return new AntennaCalibrationSession
        {
            Code = code,
            NetworkId = 1,
            SectorId = 9,
            SectorName = "مرسل",
            ReceiverName = "نقطة ميدانية",
            ReceiverLatitude = 33.53,
            ReceiverLongitude = 36.33,
            ReceiverIp = ip,
            Alignment = alignment,
            SectorAntennaMsl = 820,
            ReceiverAntennaMsl = 790
        };
    }
}
