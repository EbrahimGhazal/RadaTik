using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
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

