namespace RadaTik.Services.SectorRadio;

public interface ISectorRadioMetricsQueue
{
    ValueTask EnqueueAsync(SectorRadioMetricsJob job, CancellationToken cancellationToken = default);
    ValueTask<SectorRadioMetricsJob> DequeueAsync(CancellationToken cancellationToken = default);
}
