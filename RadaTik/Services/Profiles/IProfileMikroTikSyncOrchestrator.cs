namespace RadaTik.Services.Profiles;

public interface IProfileMikroTikSyncOrchestrator
{
    Task<ProfileSyncFromMikroTikOutcome> SyncFromMikroTikAsync(
        ProfileSyncFromMikroTikCommand command,
        CancellationToken ct = default);
}
