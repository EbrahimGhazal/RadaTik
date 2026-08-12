using System.Threading.Channels;

namespace RadaTik.Services.MikroTikSync;

/// <summary>
/// تطبيق طابور المزامنة باستخدام Channel (ذو إنتاجية عالية وآمن للخيوط)
/// </summary>
public sealed class MikroTikSyncQueue : IMikroTikSyncQueue
{
    private readonly Channel<MikroTikSyncJob> _channel = Channel.CreateUnbounded<MikroTikSyncJob>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });

    public ValueTask EnqueueAsync(MikroTikSyncJob job, CancellationToken cancellationToken = default) =>
        _channel.Writer.WriteAsync(job, cancellationToken);

    public ValueTask<MikroTikSyncJob> DequeueAsync(CancellationToken cancellationToken = default) =>
        _channel.Reader.ReadAsync(cancellationToken);
}
