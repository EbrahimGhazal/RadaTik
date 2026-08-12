using RadaTik.Models;

namespace RadaTik.Services;

public interface ICompanyProfileCatalogService
{
    Task<CompanyProfileCatalogService.CatalogOperationResult> CreateCatalogAndDeployAsync(
        Profile template,
        IReadOnlyList<int> serverIds,
        int selectedNetworkId,
        CancellationToken cancellationToken = default);

    Task<CompanyProfileCatalogService.CatalogOperationResult> DeployCatalogToServersAsync(
        int catalogId,
        IReadOnlyList<int> serverIds,
        int selectedNetworkId,
        CancellationToken cancellationToken = default);

    Task<List<MikroTikServer>> GetDeployableServersAsync(
        int selectedNetworkId,
        int? catalogId,
        CancellationToken cancellationToken = default);
}
