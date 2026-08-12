namespace RadaTik.Services.Clients;

public interface IClientWalletTopUpService
{
    Task<ClientWalletTopUpOutcome> TopUpAsync(ClientWalletTopUpCommand command, CancellationToken ct = default);

    Task<BulkClientWalletTopUpOutcome> BulkTopUpAsync(BulkClientWalletTopUpCommand command, CancellationToken ct = default);
}
