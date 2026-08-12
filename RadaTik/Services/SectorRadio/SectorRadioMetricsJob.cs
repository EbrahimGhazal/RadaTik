namespace RadaTik.Services.SectorRadio;

public sealed class SectorRadioMetricsJob
{
    public int SectorId { get; set; }
    public int MikroTikServerId { get; set; }
    public DateTime EnqueuedAt { get; set; } = DateTime.Now;
}
