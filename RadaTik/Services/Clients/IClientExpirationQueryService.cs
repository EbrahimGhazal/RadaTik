namespace RadaTik.Services.Clients;

public interface IClientExpirationQueryService
{
    Task<ClientExpiredAccountsPageModel> BuildExpiredAccountsPageAsync(int networkId, CancellationToken ct = default);

    Task<ClientExpiringSoonPageModel> BuildExpiringIn3DaysPageAsync(int networkId, CancellationToken ct = default);
}
