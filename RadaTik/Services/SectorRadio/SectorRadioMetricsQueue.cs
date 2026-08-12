using System.Threading.Channels;

namespace RadaTik.Services.SectorRadio;

public sealed class SectorRadioMetricsQueue : ISectorRadioMetricsQueue
{
    private readonly Channel<SectorRadioMetricsJob> _channel = Channel.CreateUnbounded<SectorRadioMetricsJob>(
        new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

    public ValueTask EnqueueAsync(SectorRadioMetricsJob job, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public ValueTask<SectorRadioMetricsJob> DequeueAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
