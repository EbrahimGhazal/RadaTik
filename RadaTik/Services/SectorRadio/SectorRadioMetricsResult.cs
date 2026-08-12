namespace RadaTik.Services.SectorRadio;

public sealed class SectorRadioMetricsResult
{
    public bool Success { get; set; }
    public string StatusMessage { get; set; } = string.Empty;

    public int? FrequencyMhz { get; set; }
    public int? ChannelWidthMhz { get; set; }
    public int? NoiseFloorDbm { get; set; }
    public int? SignalDbm { get; set; }
    public int? SnrDb { get; set; }
    public int? CcqPercent { get; set; }
    public decimal? TxRateMbps { get; set; }
    public decimal? RxRateMbps { get; set; }
}
