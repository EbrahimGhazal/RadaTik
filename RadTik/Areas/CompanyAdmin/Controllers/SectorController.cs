using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.PricingPolicies;
using RadTik.Services.SectorRadio;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class SectorController : RadTik.Controllers.SectorController
{
    public SectorController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ISenderPricingOrchestrator senderPricingOrchestrator,
        IMikroTikUsersService mikroTikService,
        ISectorRadioMetricsQueue sectorRadioQueue)
        : base(context, userManager, usageChargeService, senderPricingOrchestrator, mikroTikService, sectorRadioQueue)
    {
    }
}

