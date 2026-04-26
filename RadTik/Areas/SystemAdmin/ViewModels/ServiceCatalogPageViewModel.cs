using RadTik.Models;

namespace RadTik.Areas.SystemAdmin.ViewModels;

public sealed class ServiceCatalogPageViewModel
{
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

    public int MaxFreeUnitsLimit => RadTik.Services.SystemAdminPricing.RecurringPricingPolicyCodec.MaxFreeUnitsLimit;
    public string NonNegativeFreeUnitsMessage => RadTik.Services.SystemAdminPricing.RecurringPricingPolicyCodec.NonNegativeFreeUnitsMessage;
    public string MaxFreeUnitsExceededMessage => RadTik.Services.SystemAdminPricing.RecurringPricingPolicyCodec.BuildMaxFreeUnitsExceededMessage();
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
}

public sealed class MaintenanceCommissionSettingsViewModel
{
    public string ServiceName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public MaintenanceCommissionMode CommissionMode { get; set; } = MaintenanceCommissionMode.Fixed;
    public decimal CommissionValue { get; set; }
}
