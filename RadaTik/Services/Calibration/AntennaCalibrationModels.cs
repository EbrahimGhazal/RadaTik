using RadaTik.ViewModels;

namespace RadaTik.Services.Calibration;

public sealed class AntennaCalibrationSession
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
    public AntennaCalibrationPathSnapshot Path { get; init; } = AntennaCalibrationPathSnapshot.Unavailable();
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; init; } = DateTime.UtcNow.AddHours(4);
    public AntennaCalibrationEndpointPose Transmitter { get; } = new();
    public AntennaCalibrationEndpointPose Receiver { get; } = new();
    public AntennaCalibrationRadioState Radio { get; } = new();
}

public sealed class AntennaCalibrationEndpointPose
{
    public bool Connected { get; set; }
    public bool PoseValid { get; set; }
    public double? AzimuthDegrees { get; set; }
    public double? ElevationDegrees { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? ConnectionId { get; set; }
    public double? AccuracyDegrees { get; set; }
}

public sealed class AntennaCalibrationRadioState
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
    public decimal? TxRateMbps { get; set; }
    public decimal? RxRateMbps { get; set; }
    public int? NoiseFloorDbm { get; set; }
    public int? FrequencyMhz { get; set; }
    public string? MacAddress { get; set; }
    public string? LastIp { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public AntennaCalibrationRadioSnapshot ToSnapshot() => new()
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
        TxRateMbps = TxRateMbps,
        RxRateMbps = RxRateMbps,
        NoiseFloorDbm = NoiseFloorDbm,
        FrequencyMhz = FrequencyMhz,
        MacAddress = MacAddress,
        LastIp = LastIp,
        UpdatedAtUtc = UpdatedAtUtc
    };
}

public sealed class AntennaCalibrationPathSnapshot
{
    public bool Analyzed { get; init; }
    public bool PathClear { get; init; }
    public bool TerrainClear { get; init; }
    public bool FresnelClear { get; init; }
    public double FrequencyMhz { get; init; }
    public string FrequencySource { get; init; } = "default";
    public string? TerrainNote { get; init; }
    public int BuildingsConsidered { get; init; }
    public int VegetationConsidered { get; init; }
    public required string Summary { get; init; }

    public static AntennaCalibrationPathSnapshot Unavailable(string? message = null) => new()
    {
        Analyzed = false,
        Summary = message ?? "تعذر تحليل خط الرؤية. المعايرة الهندسية ما زالت صالحة."
    };

    public static AntennaCalibrationPathSnapshot From(LineOfSightResult los)
    {
        if (!los.Success)
        {
            return Unavailable(los.ErrorMessage);
        }

        string summary;
        if (los.PathClear)
        {
            summary = "المسار مفتوح تقريباً. اضبط البوصلة ثم ثبّت عند قمة الإشارة.";
        }
        else if (!los.TerrainClear)
        {
            summary = "التضاريس تقطع الخط. توجيه الهوائي لن يفتح المسار.";
        }
        else if (!los.FresnelClear)
        {
            summary = "منطقة فريسنل غير خالية. قد تضعف الإشارة حتى مع محاذاة صحيحة.";
        }
        else
        {
            summary = string.IsNullOrWhiteSpace(los.TerrainNote)
                ? "عائق محتمل على المسار. راقب الإشارة بعد المحاذاة."
                : los.TerrainNote;
        }

        return new AntennaCalibrationPathSnapshot
        {
            Analyzed = true,
            PathClear = los.PathClear,
            TerrainClear = los.TerrainClear,
            FresnelClear = los.FresnelClear,
            FrequencyMhz = los.FrequencyMhzUsed,
            FrequencySource = los.FrequencySource,
            TerrainNote = los.TerrainNote,
            BuildingsConsidered = los.BuildingsConsidered,
            VegetationConsidered = los.VegetationConsidered,
            Summary = summary
        };
    }
}

public sealed class AntennaCalibrationSnapshot
{
    public required string Code { get; init; }
    public required string SectorName { get; init; }
    public required string ReceiverName { get; init; }
    public required bool SavedReceiver { get; init; }
    public required double DistanceMeters { get; init; }
    public required double MagneticDeclinationDegrees { get; init; }
    public required AntennaCalibrationTargetSnapshot TransmitterTarget { get; init; }
    public required AntennaCalibrationTargetSnapshot ReceiverTarget { get; init; }
    public required AntennaCalibrationLiveSnapshot TransmitterLive { get; init; }
    public required AntennaCalibrationLiveSnapshot ReceiverLive { get; init; }
    public required AntennaCalibrationPathSnapshot Path { get; init; }
    public required AntennaCalibrationRadioSnapshot Radio { get; init; }
    public required bool MutualFacing { get; init; }
    public required bool GeometryLocked { get; init; }
    public required string Advice { get; init; }
}

public sealed class AntennaCalibrationTargetSnapshot
{
    public required double AzimuthTrue { get; init; }
    public required double AzimuthMagnetic { get; init; }
    public required double Elevation { get; init; }
    public required string Cardinal { get; init; }
}

public sealed class AntennaCalibrationLiveSnapshot
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

public sealed class AntennaCalibrationRadioSnapshot
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
    public decimal? TxRateMbps { get; init; }
    public decimal? RxRateMbps { get; init; }
    public int? NoiseFloorDbm { get; init; }
    public int? FrequencyMhz { get; init; }
    public string? MacAddress { get; init; }
    public string? LastIp { get; init; }
    public DateTime? UpdatedAtUtc { get; init; }
}

public sealed class AntennaCalibrationSessionListItem
{
    public required string Code { get; init; }
    public required string SectorName { get; init; }
    public required string ReceiverName { get; init; }
    public required DateTime CreatedAtUtc { get; init; }
}
