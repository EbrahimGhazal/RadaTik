using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using RadTik.Security;

namespace RadTik.Areas.ClientPortal.Controllers
{
    /// <summary>
    /// ClientPortal Area wrapper around existing client portal logic.
    /// Exposes the same controller under /clientPortal/{action}.
    /// </summary>
    [Area("ClientPortal")]
    [Authorize(Roles = RoleNames.Client)]
    public class ClientPortalController : global::RadTik.Controllers.ClientPortalController
    {
        public ClientPortalController(
            RadTik.Data.ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<RadTik.Models.ApplicationUser> userManager,
            ILogger<global::RadTik.Controllers.ClientPortalController> logger,
            RadTik.Services.RequestNotificationService requestNotificationService,
            RadTik.Services.IMaintenanceBillingService maintenanceBillingService,
            RadTik.Services.IClientRenewalGuardService clientRenewalGuardService,
            IWebHostEnvironment environment,
            RadTik.Services.IMikroTikUsersService mikroTikService)
            : base(context, userManager, logger, requestNotificationService, maintenanceBillingService, clientRenewalGuardService, environment, mikroTikService)
        {
        }
    }
}

