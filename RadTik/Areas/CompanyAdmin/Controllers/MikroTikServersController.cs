using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.CompanyAdmin.Controllers
{
    /// <summary>
    /// CompanyAdmin Area wrapper around existing controller logic.
    /// Keeps behavior intact while organizing routes under /networkManager.
    /// </summary>
    [Area("CompanyAdmin")]
    [Authorize(Roles = RoleNames.NetworkAdministrator + "," + RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.MikroTikServers)]
    public class MikroTikServersController : global::RadTik.Controllers.MikroTikServersController
    {
        public MikroTikServersController(
            ApplicationDbContext context,
            IMikroTikUsersService mikroTikService,
            IMikroTikProfilesService mikroTikProfilesService,
            ILogger<global::RadTik.Controllers.MikroTikServersController> logger,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService)
            : base(context, mikroTikService, mikroTikProfilesService, logger, userManager, usageChargeService)
        {
        }
    }
}

