namespace RadaTik.Services.Clients;

public interface IClientSelfRenewalService
{
    Task<ClientOperationOutcome> RenewFromWalletAsync(int clientId, CancellationToken ct = default);
}
