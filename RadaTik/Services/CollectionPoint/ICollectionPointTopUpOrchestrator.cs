namespace RadaTik.Services.CollectionPoint;

public sealed record TopUpClientBalanceCommand(
    int ClientId,
    int NetworkId,
    string UserId,
    string? UserDisplayName,
    decimal Amount,
    decimal? ExchangeRate,
    string? Notes);

public interface ICollectionPointTopUpOrchestrator
{
    Task<CollectionPointOperationOutcome> TopUpAsync(TopUpClientBalanceCommand command, CancellationToken ct = default);
}
