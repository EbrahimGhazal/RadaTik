namespace RadaTik.Services.Clients;

public sealed record ServerPppSessionsSnapshot(
    int ServerId,
    IReadOnlyList<string> Names,
    DateTimeOffset CapturedAt,
    bool Succeeded,
    DateTimeOffset? LastFailureAt);

public sealed record CompanyLiveConnectionSnapshot(
    int CompanyScopeId,
    HashSet<int> ConnectedClientIds,
    DateTimeOffset CapturedAt,
    bool Ready);

public sealed class ClientLiveConnectionStatus
{
    public bool Ready { get; init; }
    public HashSet<int> ConnectedClientIds { get; init; } = [];
    public DateTimeOffset? CapturedAt { get; init; }
}
