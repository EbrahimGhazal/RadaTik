namespace RadaTik.Services.Clients;

public interface IClientImportOrchestrator
{
    Task<ClientImportPageModel> BuildImportPageAsync(int networkId, CancellationToken ct = default);

    Task<ClientImportFromServerViewModel> BuildImportFromServerViewAsync(int networkId, CancellationToken ct = default);

    Task<MikroTikServerUsersImportContext> BuildServerUsersImportContextAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default);

    Task<ClientImportOutcome> ExecuteImportAsync(
        int serverId,
        int networkId,
        string actorUserId,
        bool rejectWhenProfilesMissing,
        CancellationToken ct = default);
}
