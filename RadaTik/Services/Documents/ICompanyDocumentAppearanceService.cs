using RadaTik.Models;

namespace RadaTik.Services.Documents;

public interface ICompanyDocumentAppearanceService
{
    Task<int?> ResolveCompanyNetworkIdAsync(int selectedNetworkId, CancellationToken ct = default);

    Task<CompanyDocumentChrome> GetChromeAsync(
        int selectedOrCompanyNetworkId,
        string? documentTitle = null,
        string? subtitle = null,
        string? generatedAt = null,
        CancellationToken ct = default);

    Task<CompanyDocumentAppearanceEditor> GetEditorAsync(int companyNetworkId, CancellationToken ct = default);

    Task SaveAsync(
        int companyNetworkId,
        string userId,
        CompanyDocumentAppearanceSaveCommand command,
        CancellationToken ct = default);
}
