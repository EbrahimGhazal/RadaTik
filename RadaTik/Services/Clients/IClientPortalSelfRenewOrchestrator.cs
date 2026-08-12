namespace RadaTik.Services.Clients;

public interface IClientPortalSelfRenewOrchestrator
{
    Task<ClientPortalSelfRenewOutcome> ExecuteAsync(ClientPortalSelfRenewCommand command, CancellationToken ct = default);
}
