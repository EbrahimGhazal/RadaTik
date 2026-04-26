namespace RadTik.ViewModels.Sector;

public sealed class SectorRadioMonitoringViewModel
{
    public int TotalSectors { get; set; }
    public int MetricsFreshCount { get; set; }
    public int StaleCount { get; set; }
    public int ActiveAlertsCount { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.Now;
    public List<SectorRadioTrendPoint> TrendPoints { get; set; } = [];
    public List<SectorRadioEventRow> RecentEvents { get; set; } = [];
    public List<SectorRadioMonitoringRow> Rows { get; set; } = [];
}

public sealed class SectorRadioMonitoringRow
{
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string? SectorIp { get; set; }
    public string ServerName { get; set; } = string.Empty;
    public DateTime? CapturedAt { get; set; }
    public int? FrequencyMhz { get; set; }
    public int? NoiseFloorDbm { get; set; }
    public int? SignalDbm { get; set; }
    public int? SnrDb { get; set; }
    public int? CcqPercent { get; set; }
    public string? LastSeverity { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}

public sealed class SectorRadioTrendPoint
{
    public DateTime BucketAt { get; set; }
    public decimal? AvgSnrDb { get; set; }
    public decimal? AvgNoiseDbm { get; set; }
    public decimal? AvgCcqPercent { get; set; }
}

public sealed class SectorRadioEventRow
{
    public long Id { get; set; }
    public int SectorId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning";
    public string MetricName { get; set; } = string.Empty;
    public decimal? MetricValue { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
