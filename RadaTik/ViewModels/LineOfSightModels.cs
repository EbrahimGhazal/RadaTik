namespace RadaTik.ViewModels;

/// <summary>طلب تحليل خط الرؤية من واجهة إنشاء/تعديل المستقبل (JSON).</summary>
public sealed class AnalyzeLineOfSightRequest
{
    public int SectorId { get; set; }
    public double ReceiverLatitude { get; set; }
    public double ReceiverLongitude { get; set; }
    /// <summary>اختياري: إن وُجد يُستخدم بدل جلب الارتفاع من الخدمة عند المستقبل.</summary>
    public double? ReceiverElevationMeters { get; set; }
    /// <summary>اختياري: ارتفاع الهوائي عند المستقبل (م).</summary>
    public double? ReceiverAntennaHeightAglMeters { get; set; }
    /// <summary>اختياري: تردد الراديو بالميغاهرتز لحساب فريسنل.</summary>
    public double? FrequencyMhz { get; set; }
}

public sealed class LineOfSightAnalysisInput
{
    public double SectorLat { get; init; }
    public double SectorLon { get; init; }
    /// <summary>ارتفاع سطح الأرض عند المرسل (متر عن سطح البحر).</summary>
    public double? SectorTerrainElevationMeters { get; init; }
    /// <summary>ارتفاع الهوائي عن الأرض عند المرسل (م).</summary>
    public double SectorAntennaAglMeters { get; init; } = 12;

    public double ReceiverLat { get; init; }
    public double ReceiverLon { get; init; }
    public double? ReceiverTerrainElevationMeters { get; init; }
    public double ReceiverAntennaAglMeters { get; init; } = 6;

    public int SampleCount { get; init; } = 48;

    /// <summary>تردد الراديو بالميغاهرتز لحساب فريسنل. 0 = افتراضي 5800.</summary>
    public double FrequencyMhz { get; init; }
    /// <summary>user | sector | default</summary>
    public string FrequencySource { get; init; } = "default";
}

public sealed class LineOfSightResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }

    public double DistanceMeters { get; init; }

    /// <summary>هل المسار خالٍ تقريباً (تضاريس + فريسنل + مبانٍ).</summary>
    public bool PathClear { get; init; }

    /// <summary>هل خط الرؤية فوق التضاريس بهامش هندسي كافٍ (بدون فريسنل/مبانٍ).</summary>
    public bool TerrainClear { get; init; }

    /// <summary>هل 60٪ من منطقة فريسنل الأولى خالية من التضاريس والمبانٍ المفحوصة.</summary>
    public bool FresnelClear { get; init; }

    public double MinTerrainMarginMeters { get; init; }
    public double MinFresnelMarginMeters { get; init; }
    public string? TerrainNote { get; init; }

    public double FrequencyMhzUsed { get; init; }
    public bool FrequencyIsDefault { get; init; }
    /// <summary>user | sector | default</summary>
    public string FrequencySource { get; init; } = "default";
    public bool EarthCurvatureApplied { get; init; }

    public bool BuildingsDataAvailable { get; init; }
    public int BuildingsConsidered { get; init; }
    public int VegetationConsidered { get; init; }
    public int BuildingsMeasuredHeightCount { get; init; }
    public int BuildingsEstimatedHeightCount { get; init; }
    public IReadOnlyList<BuildingObstructionInfo> BuildingObstructions { get; init; } = [];

    public IReadOnlyList<LosProfilePoint> Profile { get; init; } = [];
}

public sealed class LosProfilePoint
{
    public double DistanceFromStartMeters { get; init; }
    public double TerrainElevationMslMeters { get; init; }
    public double EarthBulgeMeters { get; init; }
    public double EffectiveTerrainMslMeters { get; init; }
    public double LineHeightMslMeters { get; init; }
    public double FresnelRadiusMeters { get; init; }
    public double FresnelFloorMslMeters { get; init; }
    public double MarginMeters { get; init; }
    public double FresnelMarginMeters { get; init; }
}

public sealed class BuildingObstructionInfo
{
    public double Lat { get; init; }
    public double Lon { get; init; }
    public double EstimatedBuildingHeightMeters { get; init; }
    public string HeightSource { get; init; } = "building_type";
    public string HeightConfidence { get; init; } = "low";
    public string ObstacleKind { get; init; } = "building";
    public string? BuildingType { get; init; }
    public double GroundElevationMslMeters { get; init; }
    public double RoofMslMeters { get; init; }
    public double PathFraction { get; init; }
    public double CrossTrackMeters { get; init; }
    public bool IntersectsPath { get; init; }
    public double LineHeightAtPointMslMeters { get; init; }
    public double FresnelRadiusMeters { get; init; }
    public bool LikelyBlocksLos { get; init; }
    public bool LikelyBlocksFresnel { get; init; }
}
