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
public class ReceiverController : RadTik.Controllers.ReceiverController
{
    public ReceiverController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ILineOfSightAnalysisService lineOfSightAnalysisService)
        : base(context, userManager, usageChargeService, lineOfSightAnalysisService)
    {
    }
}

