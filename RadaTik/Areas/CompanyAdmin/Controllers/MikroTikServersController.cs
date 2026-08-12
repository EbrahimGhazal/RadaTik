using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.Clients;
using global::RadaTik.Services.MikroTik;
using global::RadaTik.Services.PricingPreview;

namespace RadaTik.Areas.CompanyAdmin.Controllers
{
    /// <summary>
    /// CompanyAdmin Area wrapper around existing controller logic.
    /// Keeps behavior intact while organizing routes under /networkManager.
    /// </summary>
    [Area("CompanyAdmin")]
    [Authorize(Roles = RoleNames.NetworkAdministrator + "," + RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.MikroTikServers)]
    public class MikroTikServersController : global::RadaTik.Controllers.MikroTikServersController
    {
        public MikroTikServersController(
            ApplicationDbContext context,
            IMikroTikUsersService mikroTikService,
            IMikroTikProfilesService mikroTikProfilesService,
            IClientImportOrchestrator clientImport,
            ILogger<global::RadaTik.Controllers.MikroTikServersController> logger,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ICreatePricingPreviewService pricingPreviewService)
            : base(
                context,
                mikroTikService,
                mikroTikService,
                clientImport,
                mikroTikProfilesService,
                logger,
                userManager,
                usageChargeService,
                pricingPreviewService)
        {
        }
    }
}
