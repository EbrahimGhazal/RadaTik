namespace RadaTik.Services.Clients;

public sealed class ClientFormProfileOption
{
    public int Id { get; init; }
    public string? Name { get; init; }
}

public sealed class ClientFormReceiverOption
{
    public int Id { get; init; }
    public string? Name { get; init; }
    public string? SectorName { get; init; }
}

public interface IClientFormLookupService
{
    Task<IReadOnlyList<ClientFormProfileOption>> GetProfilesByServerAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default);

    Task<bool> ProfileBelongsToServerAsync(
        int profileId,
        int serverId,
        int networkId,
        CancellationToken ct = default);

    Task<IReadOnlyList<ClientFormReceiverOption>> GetReceiversByServerAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default);
}
