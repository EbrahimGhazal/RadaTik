using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class NetworkController : RadTik.Controllers.NetworkController
{
    public NetworkController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<RadTik.Controllers.NetworkController> logger,
        IWebHostEnvironment environment,
        IUsageBasedSubscriptionChargeService usageSubscriptionChargeService)
        : base(context, userManager, logger, environment, usageSubscriptionChargeService)
    {
    }
}

