using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPolicies;
using global::RadaTik.Services.PricingPreview;
using global::RadaTik.Services.MikroTik;
using global::RadaTik.Services.SectorRadio;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class SectorController : global::RadaTik.Controllers.SectorController
{
    public SectorController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ICreatePricingPreviewService pricingPreviewService,
        ISenderPricingOrchestrator senderPricingOrchestrator,
        IMikroTikSectorService mikroTikSectorService,
        ISectorRadioMetricsQueue sectorRadioQueue,
        ILineOfSightAnalysisService lineOfSight)
        : base(context, userManager, usageChargeService, pricingPreviewService, senderPricingOrchestrator, mikroTikSectorService, sectorRadioQueue, lineOfSight)
    {
    }
}

