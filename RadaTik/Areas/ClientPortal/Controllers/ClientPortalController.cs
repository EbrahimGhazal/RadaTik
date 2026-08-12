using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Security;

namespace RadaTik.Areas.ClientPortal.Controllers
{
    /// <summary>
    /// ClientPortal Area wrapper around existing client portal logic.
    /// Exposes the same controller under /clientPortal/{action}.
    /// </summary>
    [Area("ClientPortal")]
    [Authorize(Roles = RoleNames.Client)]
    public class ClientPortalController : global::RadaTik.Controllers.ClientPortalController
    {
        public ClientPortalController(
            global::RadaTik.Data.ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<global::RadaTik.Models.ApplicationUser> userManager,
            ILogger<global::RadaTik.Controllers.ClientPortalController> logger,
            global::RadaTik.Services.IRequestNotificationService requestNotificationService,
            global::RadaTik.Services.IMaintenanceBillingService maintenanceBillingService,
            global::RadaTik.Services.IClientRenewalGuardService clientRenewalGuardService,
            global::RadaTik.Services.ICollectionCommissionChargeService collectionCommissionChargeService,
            global::RadaTik.Services.Clients.IClientPortalSelfRenewOrchestrator clientPortalSelfRenew,
            IWebHostEnvironment environment,
            global::RadaTik.Services.MikroTik.IMikroTikPppoeUserService mikroTikService)
            : base(context, userManager, logger, requestNotificationService, maintenanceBillingService, clientRenewalGuardService, collectionCommissionChargeService, clientPortalSelfRenew, environment, mikroTikService)
        {
        }
    }
}

