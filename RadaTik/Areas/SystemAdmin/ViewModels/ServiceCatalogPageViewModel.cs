using global::RadaTik.Models;

namespace RadaTik.Areas.SystemAdmin.ViewModels;

public sealed class ServiceCatalogDocumentationItemViewModel
{
    public string FeatureKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? DetailHtml { get; set; }
    public string? PricingPolicyHtml { get; set; }
    public string? RenewalPolicyHtml { get; set; }
    public string PricingPlansSummaryHtml { get; init; } = string.Empty;
    public string? SuggestedRenewalPolicyHtml { get; init; }
}

public sealed class ServiceCatalogPageViewModel
{
    public List<ServiceCatalogDocumentationItemViewModel> DocumentationItems { get; init; } = [];

    public List<SystemService> Items { get; init; } = [];

    public ServicePricingCardViewModel NetworkPricing { get; init; } = new();
    public ServicePricingCardViewModel ServerPricing { get; init; } = new();
    public ServicePricingCardViewModel SectorPricing { get; init; } = new();
    public ServicePricingCardViewModel ReceiverPricing { get; init; } = new();
    public ServicePricingCardViewModel ClientPricing { get; init; } = new();
    public ServicePricingCardViewModel UserPricing { get; init; } = new();
    public ServicePricingCardViewModel SpeedProfilePricing { get; init; } = new();
    public ServicePricingCardViewModel ReportPricing { get; init; } = new();

    public FlatServicePriceViewModel ProfilePriceTax { get; init; } = new();
    public MaintenanceCommissionSettingsViewModel MaintenanceCommission { get; init; } = new();

    public IReadOnlyList<PricingBillingPeriod> RenewalPeriods { get; } =
        Enum.GetValues<PricingBillingPeriod>()
            .Where(p => p != PricingBillingPeriod.OneTime)
            .ToList();

    public int PricedServiceCount => 7;

    public int ConfiguredRecurringCount => new[]
    {
        NetworkPricing.HasRenewalPricing,
        ServerPricing.HasRenewalPricing,
        SectorPricing.HasRenewalPricing,
        ReceiverPricing.HasRenewalPricing,
        ClientPricing.HasRenewalPricing,
        UserPricing.HasRenewalPricing,
        SpeedProfilePricing.HasRenewalPricing
    }.Count(v => v);

    public int ConfiguredOneTimeCount => new[]
    {
        NetworkPricing.HasInitialPricing,
        ServerPricing.HasInitialPricing,
        SectorPricing.HasInitialPricing,
        ReceiverPricing.HasInitialPricing,
        ClientPricing.HasInitialPricing,
        UserPricing.HasInitialPricing,
        SpeedProfilePricing.HasInitialPricing,
        ReportPricing.HasInitialPricing
    }.Count(v => v);

    public string MaintenanceCommissionModeLabel =>
        MaintenanceCommission.CommissionMode == MaintenanceCommissionMode.Percent ? "نسبة مئوية" : "مبلغ ثابت";

    public int MaxFreeUnitsLimit => global::RadaTik.Services.SystemAdminPricing.RecurringPricingPolicyCodec.MaxFreeUnitsLimit;
    public string NonNegativeFreeUnitsMessage => global::RadaTik.Services.SystemAdminPricing.RecurringPricingPolicyCodec.NonNegativeFreeUnitsMessage;
    public string MaxFreeUnitsExceededMessage => global::RadaTik.Services.SystemAdminPricing.RecurringPricingPolicyCodec.BuildMaxFreeUnitsExceededMessage();
}

public sealed class ServicePricingCardViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal InitialPriceSyp { get; set; }
    public PricingBillingPeriod RenewalBillingPeriod { get; set; } = PricingBillingPeriod.Monthly;
    public decimal RenewalPricePerUnitSyp { get; set; }
    public bool HasInitialPricing { get; set; }
    public bool HasRenewalPricing { get; set; }
    public int FreeInitialUnits { get; set; }
    public int FreeRenewalUnits { get; set; }
}

public sealed class FlatServicePriceViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool HasPricing { get; set; }
}

public sealed class MaintenanceCommissionSettingsViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MaintenanceCommissionMode CommissionMode { get; set; } = MaintenanceCommissionMode.Fixed;
    public decimal CommissionValue { get; set; }
}
