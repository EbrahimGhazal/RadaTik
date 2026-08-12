using RadaTik.Models;
using RadaTik.ViewModels.Profile;

namespace RadaTik.Services.Profiles;

public sealed class ProfileIndexPageModel
{
    public required IReadOnlyList<Profile> Profiles { get; init; }
    public required IReadOnlyList<MikroTikServer> Servers { get; init; }
    public int? SelectedServerId { get; init; }
    public decimal ProfileImportUnitPrice { get; init; }
    public required IReadOnlyList<CompanyCatalogSummaryItem> CompanyCatalogs { get; init; }
    public int TotalProfiles { get; init; }
    public int ActiveProfiles { get; init; }
    public int SyncedProfiles { get; init; }
}

public sealed class ProfileImportPreviewJsonModel
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public int ServerId { get; init; }
    public int TotalProfiles { get; init; }
    public int ImportableProfiles { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal TotalCharge { get; init; }
    public decimal WalletBalance { get; init; }
}
