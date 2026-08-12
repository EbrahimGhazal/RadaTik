using System.ComponentModel;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.PricingPreview;

namespace RadaTik.Services.Profiles;

public sealed class ProfileFormViewDataService(
    ApplicationDbContext context,
    ICompanyProfileCatalogService catalogService,
    IProfileImportPricingService profileImportPricing,
    IProfileCompanyWalletService profileCompanyWallet,
    ICreatePricingPreviewService pricingPreviewService)
    : ApplicationServiceBase(context), IProfileFormViewDataService
{
    public async Task<ProfileCreateFormViewData> BuildCreateFormDataAsync(int? networkId, CancellationToken ct = default)
    {
        if (!networkId.HasValue)
        {
            List<MikroTikServer> allServers = await Db.MikroTikServers.Where(s => s.IsActive).ToListAsync(ct);
            return new ProfileCreateFormViewData
            {
                MikroTikServers = allServers,
                UseCompanyProfileCatalog = false,
                ProfileCreateUnitPrice = 0m,
                ProfileCreateChargeHasPricing = false,
                ProfileCreateChargeAmount = 0m,
                ProfileCreateWalletBalance = 0m,
                SystemProfileVatPercentage = 15m,
                FieldDescriptions = BuildFieldDescriptions()
            };
        }

        List<MikroTikServer> servers = await catalogService.GetDeployableServersAsync(networkId.Value, null, ct);
        decimal vat = await profileCompanyWallet.ResolveSystemProfileVatPercentageAsync(ct);

        Network? selectedNetwork = await Db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value, ct);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
        ProfileImportChargeEstimate estimate =
            await profileImportPricing.CalculateProfileChargeAsync(companyNetworkId, 1, ct);

        CreatePricingPreviewResult preview = await pricingPreviewService.BuildAsync(
            networkId.Value,
            FeatureKeys.Profiles,
            PricingChargeUnit.PerSpeedProfile,
            PricingPreviewCounterKeys.Profiles,
            ct);

        return new ProfileCreateFormViewData
        {
            MikroTikServers = servers,
            UseCompanyProfileCatalog = true,
            ProfileCreateUnitPrice = estimate.UnitPrice,
            ProfileCreateChargeHasPricing = preview.HasInitialPricing,
            ProfileCreateChargeAmount = preview.ShouldChargeNow ? estimate.TotalCharge : 0m,
            ProfileCreateWalletBalance = estimate.WalletBalance,
            SystemProfileVatPercentage = vat,
            PricingPreview = preview,
            FieldDescriptions = BuildFieldDescriptions()
        };
    }

    public async Task<ProfileEditFormViewData> BuildEditFormDataAsync(int? networkId, CancellationToken ct = default)
    {
        List<MikroTikServer> servers = networkId.HasValue
            ? await Db.MikroTikServers.Where(s => s.IsActive && s.NetworkId == networkId.Value).ToListAsync(ct)
            : await Db.MikroTikServers.Where(s => s.IsActive).ToListAsync(ct);

        decimal vat = networkId.HasValue
            ? await profileCompanyWallet.ResolveSystemProfileVatPercentageAsync(ct)
            : 15m;

        return new ProfileEditFormViewData
        {
            MikroTikServers = servers,
            SystemProfileVatPercentage = vat,
            FieldDescriptions = BuildFieldDescriptions()
        };
    }

    private static IReadOnlyDictionary<string, string?> BuildFieldDescriptions()
    {
        Dictionary<string, string?> descriptions = new(StringComparer.Ordinal);
        foreach (PropertyInfo prop in typeof(Profile).GetProperties())
        {
            if (prop.GetCustomAttribute<DescriptionAttribute>() is DescriptionAttribute descriptionAttr)
            {
                descriptions[prop.Name] = descriptionAttr.Description;
            }
        }

        return descriptions;
    }
}
