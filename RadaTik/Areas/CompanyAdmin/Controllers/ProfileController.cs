using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPreview;
using global::RadaTik.Services.Profiles;

namespace RadaTik.Areas.CompanyAdmin.Controllers
{
    /// <summary>
    /// CompanyAdmin Area wrapper around existing controller logic.
    /// Keeps behavior intact while organizing routes under /networkManager.
    /// </summary>
    [Area("CompanyAdmin")]
    [Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Profiles)]
    public class ProfileController : global::RadaTik.Controllers.ProfileController
    {
        public ProfileController(
            ApplicationDbContext context,
            IMikroTikProfilesService mikroTikService,
            ILogger<global::RadaTik.Controllers.ProfileController> logger,
            UserManager<ApplicationUser> userManager,
            ICreatePricingPreviewService pricingPreviewService,
            ICompanyProfileCatalogService catalogService,
            IProfileImportPricingService profileImportPricing,
            IProfileListQueryService profileListQuery,
            IProfileImportPreviewService profileImportPreview,
            IProfileFormViewDataService profileFormViewData,
            IProfileCompanyWalletService profileCompanyWallet,
            IProfileMikroTikSyncOrchestrator profileMikroTikSync)
            : base(
                context,
                mikroTikService,
                logger,
                userManager,
                pricingPreviewService,
                catalogService,
                profileImportPricing,
                profileListQuery,
                profileImportPreview,
                profileFormViewData,
                profileCompanyWallet,
                profileMikroTikSync)
        {
        }
    }
}

