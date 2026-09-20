using RadaTik.ViewModels;

namespace RadaTik.Services.Calibration;

public sealed class CalibrationSession
{
    public required string Code { get; init; }
    public required int NetworkId { get; init; }
    public required int SectorId { get; init; }
    public int? ReceiverId { get; init; }
    public required string SectorName { get; init; }
    public required string ReceiverName { get; init; }
    public required double ReceiverLatitude { get; init; }
    public required double ReceiverLongitude { get; init; }
    public string? ReceiverIp { get; init; }
    public string? ReceiverMac { get; init; }
    public required AntennaAlignmentResult Alignment { get; init; }
    public required double SectorAntennaMsl { get; init; }
    public required double ReceiverAntennaMsl { get; init; }
    public string PathSummary { get; init; } = string.Empty;
    public bool PathClear { get; init; } = true;
    public required CalibrationScenarioSnapshot Scenario { get; init; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; init; } = DateTime.UtcNow.AddHours(4);
    public CalibrationEndpointPose Transmitter { get; } = new();
    public CalibrationEndpointPose Receiver { get; } = new();
    public CalibrationRadioState Radio { get; } = new();
}

public sealed class CalibrationEndpointPose
{
    public bool Connected { get; set; }
    public bool PoseValid { get; set; }
    public double? AzimuthDegrees { get; set; }
    public double? ElevationDegrees { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? ConnectionId { get; set; }
    public double? AccuracyDegrees { get; set; }
}

public sealed class CalibrationRadioState
{
    public bool Available { get; set; }
    public bool Stale { get; set; }
    public string Status { get; set; } = "بانتظار قراءة الراديو.";
    public string? MatchReason { get; set; }
    public int? SignalDbm { get; set; }
    public int? PeakSignalDbm { get; set; }
    public int? SnrDb { get; set; }
    public int? PeakSnrDb { get; set; }
    public int? CcqPercent { get; set; }
    public int? NoiseFloorDbm { get; set; }
    public int? FrequencyMhz { get; set; }
    public string? MacAddress { get; set; }
    public string? LastIp { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? NearPeakSinceUtc { get; set; }

    public CalibrationRadioSnapshot ToSnapshot(CalibrationScenarioSnapshot scenario, DateTime? utcNow = null)
    {
        DateTime now = utcNow ?? DateTime.UtcNow;
        int holdSeconds = Math.Max(1, scenario.PeakHoldSeconds);
        bool nearPeak = SignalDbm is int live && PeakSignalDbm is int peak && live >= peak - 2;
        bool peakLocked = nearPeak
            && NearPeakSinceUtc is DateTime since
            && now - since >= TimeSpan.FromSeconds(holdSeconds);
        bool minReached = scenario.MinSignalDbm is int min
            && SignalDbm is int s
            && s >= min;
        string success = CalibrationOptionValues.NormalizeSuccess(scenario.SuccessMode);
        bool successMet = success switch
        {
            CalibrationOptionValues.SuccessHold => peakLocked,
            CalibrationOptionValues.SuccessMinDbm => minReached,
            _ => nearPeak && SignalDbm is int cur && PeakSignalDbm is int p && cur >= p - 1
        };

        return new CalibrationRadioSnapshot
        {
            Available = Available,
            Stale = Stale,
            Status = Status,
            MatchReason = MatchReason,
            SignalDbm = SignalDbm,
            PeakSignalDbm = PeakSignalDbm,
            SnrDb = SnrDb,
            PeakSnrDb = PeakSnrDb,
            CcqPercent = CcqPercent,
            NoiseFloorDbm = NoiseFloorDbm,
            FrequencyMhz = FrequencyMhz,
            MacAddress = MacAddress,
            LastIp = LastIp,
            UpdatedAtUtc = UpdatedAtUtc,
            NearPeak = nearPeak,
            PeakLocked = peakLocked,
            MinReached = minReached,
            SuccessMet = successMet
        };
    }
}

public sealed class CalibrationSnapshot
{
    public required string Code { get; init; }
    public required string SectorName { get; init; }
    public required string ReceiverName { get; init; }
    public required bool SavedReceiver { get; init; }
    public required CalibrationScenarioSnapshot Scenario { get; init; }
    public required string ScenarioLabel { get; init; }
    public required double DistanceMeters { get; init; }
    public required double MagneticDeclinationDegrees { get; init; }
    public string? ExpectedSignalHint { get; init; }
    public required CalibrationTargetSnapshot TransmitterTarget { get; init; }
    public required CalibrationTargetSnapshot ReceiverTarget { get; init; }
    public required CalibrationLiveSnapshot TransmitterLive { get; init; }
    public required CalibrationLiveSnapshot ReceiverLive { get; init; }
    public required string PathSummary { get; init; }
    public required bool PathClear { get; init; }
    public required CalibrationRadioSnapshot Radio { get; init; }
    public required bool MutualFacing { get; init; }
    public required bool GeometryLocked { get; init; }
    public required string Advice { get; init; }
}

public sealed class CalibrationTargetSnapshot
{
    public required double AzimuthTrue { get; init; }
    public required double AzimuthMagnetic { get; init; }
    public required double Elevation { get; init; }
    public required string Cardinal { get; init; }
}

public sealed class CalibrationLiveSnapshot
{
    public required bool Connected { get; init; }
    public required bool PoseValid { get; init; }
    public double? Azimuth { get; init; }
    public double? Elevation { get; init; }
    public double? AzimuthDelta { get; init; }
    public double? ElevationDelta { get; init; }
    public required bool HorizontalAligned { get; init; }
    public required bool VerticalAligned { get; init; }
    public double? AccuracyDegrees { get; init; }
    public required bool CompassUnstable { get; init; }
}

public sealed class CalibrationRadioSnapshot
{
    public required bool Available { get; init; }
    public required bool Stale { get; init; }
    public required string Status { get; init; }
    public string? MatchReason { get; init; }
    public int? SignalDbm { get; init; }
    public int? PeakSignalDbm { get; init; }
    public int? SnrDb { get; init; }
    public int? PeakSnrDb { get; init; }
    public int? CcqPercent { get; init; }
    public int? NoiseFloorDbm { get; init; }
    public int? FrequencyMhz { get; init; }
    public string? MacAddress { get; init; }
    public string? LastIp { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
    public required bool NearPeak { get; init; }
    public required bool PeakLocked { get; init; }
    public required bool MinReached { get; init; }
    public required bool SuccessMet { get; init; }
}

public sealed class CalibrationSessionListItem
{
    public required string Code { get; init; }
    public required string SectorName { get; init; }
    public required string ReceiverName { get; init; }
    public required string ScenarioName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
