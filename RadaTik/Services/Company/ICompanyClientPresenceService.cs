using System.Security.Claims;
using RadaTik.Models;

namespace RadaTik.Services.Company;

public sealed class CompanyClientPresenceSnapshot
{
    public string CompanyName { get; init; } = string.Empty;
    public IReadOnlyList<CompanySocialLink> VisibleSocialLinks { get; init; } = [];
    public IReadOnlyList<CompanyComplaintContact> VisibleComplaintContacts { get; init; } = [];
    public bool HasSocialLinks => VisibleSocialLinks.Count > 0;
    public bool HasComplaintContacts => VisibleComplaintContacts.Count > 0;
}

public sealed class CompanyClientPresenceAdminPage
{
    public int CompanyNetworkId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string Tab { get; init; } = "social";
    public IReadOnlyList<CompanySocialLink> SocialLinks { get; init; } = [];
    public IReadOnlyList<CompanyComplaintContact> ComplaintContacts { get; init; } = [];
}

public sealed class CompanySocialLinkSaveCommand
{
    public SocialMediaPlatform Platform { get; init; }
    public string? DisplayName { get; init; }
    public string? Url { get; init; }
    public bool IsVisibleToClients { get; init; } = true;
}

public sealed class CompanyComplaintContactSaveCommand
{
    public string? Label { get; init; }
    public string? PhoneNumber { get; init; }
    public bool IsVisibleToClients { get; init; } = true;
}

public interface ICompanyClientPresenceService
{
    Task<CompanyClientPresenceSnapshot> GetForCurrentClientAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<CompanyClientPresenceAdminPage?> GetAdminPageAsync(int selectedNetworkId, string? tab, CancellationToken ct = default);
    Task<(bool Ok, string Message)> AddSocialAsync(int selectedNetworkId, CompanySocialLinkSaveCommand command, CancellationToken ct = default);
    Task<(bool Ok, string Message)> UpdateSocialAsync(int selectedNetworkId, int id, CompanySocialLinkSaveCommand command, CancellationToken ct = default);
    Task<(bool Ok, string Message)> ToggleSocialAsync(int selectedNetworkId, int id, CancellationToken ct = default);
    Task<(bool Ok, string Message)> DeleteSocialAsync(int selectedNetworkId, int id, CancellationToken ct = default);
    Task<(bool Ok, string Message)> AddComplaintAsync(int selectedNetworkId, CompanyComplaintContactSaveCommand command, CancellationToken ct = default);
    Task<(bool Ok, string Message)> UpdateComplaintAsync(int selectedNetworkId, int id, CompanyComplaintContactSaveCommand command, CancellationToken ct = default);
    Task<(bool Ok, string Message)> ToggleComplaintAsync(int selectedNetworkId, int id, CancellationToken ct = default);
    Task<(bool Ok, string Message)> DeleteComplaintAsync(int selectedNetworkId, int id, CancellationToken ct = default);
}
