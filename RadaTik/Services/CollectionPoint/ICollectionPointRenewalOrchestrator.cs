namespace RadaTik.Services.CollectionPoint;

public sealed record PayBillCommand(int ClientId, string UserId);

public sealed record PayAndRenewCommand(
    int ClientId,
    int NetworkId,
    string UserId,
    string? Notes);

public interface ICollectionPointRenewalOrchestrator
{
    Task<CollectionPointOperationOutcome> PayBillAsync(PayBillCommand command, CancellationToken ct = default);

    Task<CollectionPointOperationOutcome> PayAndRenewAsync(PayAndRenewCommand command, CancellationToken ct = default);
}
