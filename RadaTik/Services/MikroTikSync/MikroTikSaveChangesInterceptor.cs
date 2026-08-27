using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services.MikroTikSync;

/// <summary>
/// يعترض SaveChanges لاكتشاف تغييرات Client و Profile ويضيفها لطابور مزامنة MikroTik
/// بعد أن تُسند قاعدة البيانات المعرّفات الحقيقية للكيانات الجديدة.
/// </summary>
public sealed class MikroTikSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly IMikroTikSyncQueue _queue;
    private readonly List<CapturedClientChange> _pendingClients = [];
    private readonly List<CapturedProfileChange> _pendingProfiles = [];

    public MikroTikSaveChangesInterceptor(IMikroTikSyncQueue queue)
    {
        _queue = queue;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        CapturePendingJobs(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        CapturePendingJobs(eventData.Context);
        return ValueTask.FromResult(result);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (result > 0)
        {
            EnqueueCapturedJobs();
        }
        else
        {
            _pendingClients.Clear();
            _pendingProfiles.Clear();
        }

        return result;
    }

    public override ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (result > 0)
        {
            EnqueueCapturedJobs(cancellationToken);
        }
        else
        {
            _pendingClients.Clear();
            _pendingProfiles.Clear();
        }

        return ValueTask.FromResult(result);
    }

    private void CapturePendingJobs(DbContext? context)
    {
        _pendingClients.Clear();
        _pendingProfiles.Clear();
        if (context == null)
        {
            return;
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Client> entry in context.ChangeTracker.Entries<Client>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            if (entry.State == EntityState.Modified && !HasMikroTikIdentityChange(entry))
            {
                continue;
            }

            if (entry.State != EntityState.Deleted && EmployeeApprovalStates.IsPendingClientCreate(entry.Entity))
            {
                continue;
            }

            _pendingClients.Add(new CapturedClientChange(entry.Entity, ToAction(entry.State)));
        }

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Profile> entry in context.ChangeTracker.Entries<Profile>())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            _pendingProfiles.Add(new CapturedProfileChange(entry.Entity, ToAction(entry.State)));
        }
    }

    private void EnqueueCapturedJobs(CancellationToken cancellationToken = default)
    {
        foreach (CapturedClientChange change in _pendingClients)
        {
            if (change.Action != MikroTikSyncAction.Delete && change.Client.Id <= 0)
            {
                continue;
            }

            _ = _queue.EnqueueAsync(new MikroTikSyncJob
            {
                EntityType = nameof(Client),
                EntityId = change.Client.Id,
                Action = change.Action,
                ServerId = change.Client.MikroTikServerId,
                UserName = change.Client.UserName
            }, cancellationToken);
        }

        foreach (CapturedProfileChange change in _pendingProfiles)
        {
            if (change.Action != MikroTikSyncAction.Delete && change.Profile.Id <= 0)
            {
                continue;
            }

            _ = _queue.EnqueueAsync(new MikroTikSyncJob
            {
                EntityType = nameof(Profile),
                EntityId = change.Profile.Id,
                Action = change.Action,
                ServerId = change.Profile.MikroTikServerId,
                ProfileName = change.Profile.Name
            }, cancellationToken);
        }

        _pendingClients.Clear();
        _pendingProfiles.Clear();
    }

    private static readonly HashSet<string> MikroTikIdentityProperties =
    [
        nameof(Client.UserName),
        nameof(Client.Password),
        nameof(Client.ProfileId),
        nameof(Client.ProfileName),
        nameof(Client.MikroTikServerId),
        nameof(Client.IsActive),
        nameof(Client.AccountExpirationDate),
        nameof(Client.Service),
        nameof(Client.Address)
    ];

    private static bool HasMikroTikIdentityChange(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<Client> entry) =>
        entry.Properties.Any(property =>
            property.IsModified && MikroTikIdentityProperties.Contains(property.Metadata.Name));

    private static MikroTikSyncAction ToAction(EntityState state) => state switch
    {
        EntityState.Added => MikroTikSyncAction.Add,
        EntityState.Deleted => MikroTikSyncAction.Delete,
        _ => MikroTikSyncAction.Update
    };

    private sealed record CapturedClientChange(Client Client, MikroTikSyncAction Action);
    private sealed record CapturedProfileChange(Profile Profile, MikroTikSyncAction Action);
}
