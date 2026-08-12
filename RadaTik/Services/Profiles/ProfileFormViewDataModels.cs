using RadaTik.Models;
using RadaTik.Services.PricingPreview;

namespace RadaTik.Services.Profiles;

public sealed class ProfileCreateFormViewData
{
    public required IReadOnlyList<MikroTikServer> MikroTikServers { get; init; }
    public bool UseCompanyProfileCatalog { get; init; }
    public decimal ProfileCreateUnitPrice { get; init; }
    public bool ProfileCreateChargeHasPricing { get; init; }
    public decimal ProfileCreateChargeAmount { get; init; }
    public decimal ProfileCreateWalletBalance { get; init; }
    public decimal SystemProfileVatPercentage { get; init; }
    public CreatePricingPreviewResult? PricingPreview { get; init; }
    public required IReadOnlyDictionary<string, string?> FieldDescriptions { get; init; }
}

public sealed class ProfileEditFormViewData
{
    public required IReadOnlyList<MikroTikServer> MikroTikServers { get; init; }
    public decimal SystemProfileVatPercentage { get; init; }
    public required IReadOnlyDictionary<string, string?> FieldDescriptions { get; init; }
}
