using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace RadaTik.Services.PricingPreview;

public static class PricingPreviewViewBagMapper
{
    public static CreatePricingPreviewResult Empty() => new();

    public static void Apply(ViewDataDictionary viewData, string prefix, CreatePricingPreviewResult preview)
    {
        viewData[$"{prefix}CompanyName"] = preview.CompanyName;
        viewData[$"{prefix}TotalCount"] = preview.TotalUnits;
        viewData[$"{prefix}HasInitial"] = preview.HasInitialPricing;
        viewData[$"{prefix}HasRenewal"] = preview.HasRenewalPricing;
        viewData[$"{prefix}InitialSyp"] = preview.InitialPriceSyp;
        viewData[$"{prefix}RenewalSyp"] = preview.RenewalPriceSyp;
        viewData[$"{prefix}RenewalPeriodLabel"] = preview.RenewalPeriodLabel;
        viewData[$"{prefix}FreeInitialUnits"] = preview.FreeInitialUnits;
        viewData[$"{prefix}FreeRenewalUnits"] = preview.FreeRenewalUnits;
        viewData[$"{prefix}ShouldChargeNow"] = preview.ShouldChargeNow;
    }
}
