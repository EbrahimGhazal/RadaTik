using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
public class ClientsController : RadTik.Controllers.ClientsController
{
    public ClientsController(
        ApplicationDbContext context,
        IMikroTikUsersService mikroTikService,
        UserManager<ApplicationUser> userManager,
        ILogger<RadTik.Controllers.ClientsController> logger,
        PermissionService permissionService,
        IUsageBasedSubscriptionChargeService usageChargeService,
        RequestNotificationService requestNotificationService,
        IClientRenewalGuardService clientRenewalGuardService)
        : base(context, mikroTikService, userManager, logger, permissionService, usageChargeService, requestNotificationService, clientRenewalGuardService)
    {
    }
}

