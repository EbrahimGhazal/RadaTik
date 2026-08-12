using RadaTik.Helpers;
using RadaTik.Services.PricingPreview;
using RadaTik.Services.Profiles;

namespace RadaTik.Controllers;

public partial class ProfileController
{
    private void ApplyCreateFormViewData(ProfileCreateFormViewData data)
    {
        ViewBag.MikroTikServers = data.MikroTikServers;
        ViewBag.UseCompanyProfileCatalog = data.UseCompanyProfileCatalog;
        ViewBag.ProfileCreateUnitPrice = data.ProfileCreateUnitPrice;
        ViewBag.ProfileCreateChargeHasPricing = data.ProfileCreateChargeHasPricing;
        ViewBag.ProfileCreateChargeAmount = data.ProfileCreateChargeAmount;
        ViewBag.ProfileCreateWalletBalance = data.ProfileCreateWalletBalance;
        ViewBag.SystemProfileVatPercentage = data.SystemProfileVatPercentage;
        ApplyFieldDescriptions(data.FieldDescriptions);
        PricingPreviewViewBagMapper.Apply(
            ViewData,
            "ProfileCreate",
            data.PricingPreview ?? PricingPreviewViewBagMapper.Empty());
    }

    private void ApplyEditFormViewData(ProfileEditFormViewData data)
    {
        ViewBag.MikroTikServers = data.MikroTikServers;
        ViewBag.SystemProfileVatPercentage = data.SystemProfileVatPercentage;
        ApplyFieldDescriptions(data.FieldDescriptions);
    }

    private void ApplyFieldDescriptions(IReadOnlyDictionary<string, string?> descriptions)
    {
        foreach (KeyValuePair<string, string?> entry in descriptions)
        {
            ViewData[$"{entry.Key}_Description"] = entry.Value;
        }
    }
}
