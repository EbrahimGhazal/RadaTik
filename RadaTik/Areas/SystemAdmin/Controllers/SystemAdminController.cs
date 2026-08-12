using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Security;

namespace RadaTik.Areas.SystemAdmin.Controllers
{
    /// <summary>
    /// SystemAdmin Area wrapper around existing system admin logic.
    /// Exposes the same controller under /systemAdmin/{action}.
    /// </summary>
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class SystemAdminController : global::RadaTik.Controllers.SystemAdminController
    {
        public SystemAdminController(
            global::RadaTik.Data.ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<global::RadaTik.Models.ApplicationUser> userManager,
            ILogger<global::RadaTik.Controllers.SystemAdminController> logger,
            IConfiguration configuration,
            global::RadaTik.Services.IOnboardingChecklistService onboardingChecklistService)
            : base(context, userManager, logger, configuration, onboardingChecklistService)
        {
        }
    }
}

