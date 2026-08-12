using global::RadaTik.Models;

namespace RadaTik.Areas.SystemAdmin.ViewModels;

public sealed class ServiceCatalogSaveViewModel
{
    public RecurringPricingFormSection Network { get; set; } = new();
    public RecurringPricingFormSection Server { get; set; } = new();
    public RecurringPricingFormSection Sector { get; set; } = new();
    public RecurringPricingFormSection Receiver { get; set; } = new();
    public RecurringPricingFormSection Client { get; set; } = new();
    public RecurringPricingFormSection User { get; set; } = new();
    public RecurringPricingFormSection SpeedProfile { get; set; } = new();

    public decimal ReportInitialPriceSyp { get; set; }

    public MaintenanceCommissionMode MaintenanceCommissionMode { get; set; } = MaintenanceCommissionMode.Fixed;
    public decimal MaintenanceCommissionValue { get; set; }

    public decimal ProfileTaxPercentage { get; set; }
}

public sealed class RecurringPricingFormSection
{
    public decimal InitialPriceSyp { get; set; }
    public PricingBillingPeriod RenewalBillingPeriod { get; set; } = PricingBillingPeriod.Monthly;
    public decimal RenewalPricePerUnitSyp { get; set; }
    public int FreeInitialUnits { get; set; }
    public int FreeRenewalUnits { get; set; }
}
