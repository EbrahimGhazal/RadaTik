using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadTik.Security;

namespace RadTik.Areas.SystemAdmin.Controllers
{
    /// <summary>
    /// SystemAdmin Area wrapper around existing system admin logic.
    /// Exposes the same controller under /systemAdmin/{action}.
    /// </summary>
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class SystemAdminController : global::RadTik.Controllers.SystemAdminController
    {
        public SystemAdminController(
            RadTik.Data.ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<RadTik.Models.ApplicationUser> userManager,
            ILogger<global::RadTik.Controllers.SystemAdminController> logger,
            IConfiguration configuration)
            : base(context, userManager, logger, configuration)
        {
        }
    }
}

