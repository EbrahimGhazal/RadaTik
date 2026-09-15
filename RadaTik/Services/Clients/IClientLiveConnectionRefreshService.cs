namespace RadaTik.Services.Clients;

public interface IClientLiveConnectionRefreshService
{
    Task RefreshAllActiveServersAsync(CancellationToken ct = default);

    Task RefreshCompanyAsync(
        int networkId,
        bool force,
        TimeSpan waitBudget,
        CancellationToken ct = default);
}
