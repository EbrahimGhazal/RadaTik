using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RadaTik.Models;

namespace RadaTik.Services.MikroTikSync;

/// <summary>
/// يعترض SaveChanges لاكتشاف تغييرات Client و Profile ويضيفها لطابور مزامنة MikroTik
/// </summary>
public sealed class MikroTikSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IMikroTikSyncQueue _queue;
    private readonly List<MikroTikSyncJob> _pendingJobs = new();

    public MikroTikSaveChangesInterceptor(IMikroTikSyncQueue queue)
    {
        _queue = queue;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CapturePendingJobs(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        CapturePendingJobs(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (result > 0 && _pendingJobs.Count > 0)
        {
            foreach (var job in _pendingJobs)
                _ = _queue.EnqueueAsync(job); // fire-and-forget للتجنب انتظار async في sync
            _pendingJobs.Clear();
        }
        return result;
    }

    public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (result > 0 && _pendingJobs.Count > 0)
        {
            foreach (var job in _pendingJobs)
                _ = _queue.EnqueueAsync(job, cancellationToken);
            _pendingJobs.Clear();
        }
        return ValueTask.FromResult(result);
    }

    private void CapturePendingJobs(DbContext? context)
    {
        _pendingJobs.Clear();
        if (context == null) return;

        foreach (var entry in context.ChangeTracker.Entries<Client>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var c = entry.Entity;
                _pendingJobs.Add(new MikroTikSyncJob
                {
                    EntityType = nameof(Client),
                    EntityId = c.Id,
                    Action = entry.State switch { EntityState.Added => MikroTikSyncAction.Add, EntityState.Deleted => MikroTikSyncAction.Delete, _ => MikroTikSyncAction.Update },
                    ServerId = c.MikroTikServerId,
                    UserName = c.UserName
                });
            }
        }

        foreach (var entry in context.ChangeTracker.Entries<Profile>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            {
                var p = entry.Entity;
                _pendingJobs.Add(new MikroTikSyncJob
                {
                    EntityType = nameof(Profile),
                    EntityId = p.Id,
                    Action = entry.State switch { EntityState.Added => MikroTikSyncAction.Add, EntityState.Deleted => MikroTikSyncAction.Delete, _ => MikroTikSyncAction.Update },
                    ServerId = p.MikroTikServerId,
                    ProfileName = p.Name
                });
            }
        }
    }
}
