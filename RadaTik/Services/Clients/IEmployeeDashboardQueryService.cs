using RadaTik.Models;

namespace RadaTik.Services.Clients;

public interface IEmployeeDashboardQueryService
{
    Task<List<Client>> GetPendingInstallationsUntilAsync(
        int networkId,
        DateTime dateInclusive,
        CancellationToken ct = default);

    Task<List<Client>> GetPendingInstallationsOnDateAsync(
        int networkId,
        DateTime date,
        CancellationToken ct = default);
}
