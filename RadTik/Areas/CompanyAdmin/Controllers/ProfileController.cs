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
    [Authorize(Roles = RoleNames.NetworkAdministrator)]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Profiles)]
    public class ProfileController : global::RadTik.Controllers.ProfileController
    {
        public ProfileController(
            ApplicationDbContext context,
            IMikroTikProfilesService mikroTikService,
            ILogger<global::RadTik.Controllers.ProfileController> logger,
            UserManager<ApplicationUser> userManager)
            : base(context, mikroTikService, logger, userManager)
        {
        }
    }
}

