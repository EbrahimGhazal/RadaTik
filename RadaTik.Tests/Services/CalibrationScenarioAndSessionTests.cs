using RadaTik.Services;
using RadaTik.Services.Calibration;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class CalibrationScenarioOptionsTests
{
    [Theory]
    [InlineData("signal", "قمة إشارة")]
    [InlineData("pro", "احترافي (أفقي→عمودي)")]
    [InlineData("geometry", "سمت/ميل هندسي")]
    public void AimLabel_UsesArabic(string aim, string expectedPart)
    {
        Assert.Contains(expectedPart, CalibrationOptionValues.AimLabel(aim));
    }

    [Fact]
    public void Templates_SeedFiveScenarios()
    {
        var rows = CalibrationScenarioTemplates.BuildDefaults(7);
        Assert.Equal(5, rows.Count);
        Assert.Contains(rows, r => r.IsDefault && r.Name.Contains("يومي"));
        Assert.Contains(rows, r => r.CrewMode == CalibrationOptionValues.CrewDual);
        Assert.Contains(rows, r => r.AimMode == CalibrationOptionValues.AimPro);
    }

    [Fact]
    public void Snapshot_FromEntity_FreezesModes()
    {
        var entity = CalibrationScenarioTemplates.BuildDefaults(1)[2];
        var snap = CalibrationScenarioSnapshot.FromEntity(entity);
        Assert.Equal(CalibrationOptionValues.AimPro, snap.AimMode);
        Assert.True(snap.IsDual);
        Assert.True(snap.NeedsCompass);
        Assert.True(snap.ShowSnrCcq);
    }
}

public sealed class CalibrationSessionStoreTests
{
    [Fact]
    public void Create_And_ToSnapshot_WorksWithoutDatabase()
    {
        var store = new CalibrationSessionStore();
        var alignment = new AntennaAlignmentResult(
            DistanceMeters: 1200,
            TransmitterAzimuthDegrees: 90,
            TransmitterElevationDegrees: -1.2,
            TransmitterAzimuthDeltaDegrees: 0,
            TransmitterCardinal: "شرق",
            ReceiverAzimuthDegrees: 270,
            ReceiverElevationDegrees: 1.2,
            ReceiverCardinal: "غرب",
            CurrentSectorAzimuthDegrees: 90,
            InsideCoverageBeam: true,
            CoverageHalfAngleDegrees: 45,
            MagneticDeclinationDegrees: 4.2,
            TransmitterMagneticAzimuthDegrees: 85.8,
            ReceiverMagneticAzimuthDegrees: 265.8,
            EarthCurvatureApplied: true);
        var session = new CalibrationSession
        {
            Code = "AB12CD",
            NetworkId = 1,
            SectorId = 9,
            SectorName = "قطاع اختبار",
            ReceiverName = "نقطة 1",
            ReceiverLatitude = 33.5,
            ReceiverLongitude = 36.3,
            Alignment = alignment,
            SectorAntennaMsl = 900,
            ReceiverAntennaMsl = 820,
            PathSummary = "المسار مفتوح تقريباً.",
            PathClear = true,
            Scenario = CalibrationScenarioSnapshot.DailyDefault()
        };

        store.Create(session);
        var loaded = store.Get("AB12CD");
        Assert.NotNull(loaded);
        var snap = store.ToSnapshot(loaded!);
        Assert.Equal("AB12CD", snap.Code);
        Assert.Equal(1200, snap.DistanceMeters);
        Assert.Contains("يومي", snap.ScenarioLabel);
        Assert.False(snap.Radio.Available);
    }

    [Fact]
    public void TryResetPeak_ClearsPeak()
    {
        var store = new CalibrationSessionStore();
        var session = MinimalSession("ZZ99AA");
        store.Create(session);
        session.Radio.PeakSignalDbm = -55;
        session.Radio.SignalDbm = -57;
        Assert.True(store.TryResetPeak("ZZ99AA"));
        var radio = store.Get("ZZ99AA")!.Radio;
        Assert.Equal(-57, radio.PeakSignalDbm);
        Assert.Null(radio.NearPeakSinceUtc);
    }

    private static CalibrationSession MinimalSession(string code) => new()
    {
        Code = code,
        NetworkId = 1,
        SectorId = 1,
        SectorName = "TX",
        ReceiverName = "RX",
        ReceiverLatitude = 33.51,
        ReceiverLongitude = 36.31,
        Alignment = new AntennaAlignmentResult(
            DistanceMeters: 500,
            TransmitterAzimuthDegrees: 10,
            TransmitterElevationDegrees: 0,
            TransmitterAzimuthDeltaDegrees: 0,
            TransmitterCardinal: "شمال",
            ReceiverAzimuthDegrees: 190,
            ReceiverElevationDegrees: 0,
            ReceiverCardinal: "جنوب",
            CurrentSectorAzimuthDegrees: 10,
            InsideCoverageBeam: true,
            CoverageHalfAngleDegrees: 30,
            MagneticDeclinationDegrees: 4,
            TransmitterMagneticAzimuthDegrees: 6,
            ReceiverMagneticAzimuthDegrees: 186,
            EarthCurvatureApplied: true),
        SectorAntennaMsl = 800,
        ReceiverAntennaMsl = 790,
        Scenario = CalibrationScenarioSnapshot.DailyDefault()
    };
}
