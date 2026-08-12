namespace RadaTik.Services.MikroTikSync;

/// <summary>
/// طابور مهام مزامنة MikroTik - يُغذَّى عند SaveChanges ويستهلكه BackgroundService
/// </summary>
public interface IMikroTikSyncQueue
{
    /// <summary>
    /// إضافة مهمة مزامنة إلى الطابور
    /// </summary>
    ValueTask EnqueueAsync(MikroTikSyncJob job, CancellationToken cancellationToken = default);

    /// <summary>
    /// استهلاك المهمة التالية (يُستدعى من BackgroundService، ينتظر حتى تصل مهمة أو إلغاء)
    /// </summary>
    ValueTask<MikroTikSyncJob> DequeueAsync(CancellationToken cancellationToken = default);
}
