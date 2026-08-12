using RadaTik.Dtos.MikroTik;

namespace RadaTik.Services.Profiles;

public sealed class ProfileSyncFromMikroTikCommand
{
    public required int ServerId { get; init; }
    public required int NetworkId { get; init; }
    public required string ActorUserId { get; init; }
    public bool ImportAsInactive { get; init; }
    public decimal DefaultPrice { get; init; }
}

public enum ProfileSyncFromMikroTikStatus
{
    Success,
    Info,
    NoImportable,
    InsufficientBalance,
    ServerNotFound,
    SyncFailed,
    Error
}

public sealed class ProfileSyncFromMikroTikOutcome
{
    public ProfileSyncFromMikroTikStatus Status { get; init; }
    public string? Message { get; init; }
    public decimal ChargedAmount { get; init; }
    public SyncResult? SyncResult { get; init; }
}
