using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPreview;
using global::RadaTik.Services.SectorRadio;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class ReceiverController : global::RadaTik.Controllers.ReceiverController
{
    public ReceiverController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ICreatePricingPreviewService pricingPreviewService,
        ILineOfSightAnalysisService lineOfSightAnalysisService,
        ISectorRadioAdapter sectorRadioAdapter,
        IMemoryCache memoryCache)
        : base(context, userManager, usageChargeService, pricingPreviewService, lineOfSightAnalysisService, sectorRadioAdapter, memoryCache)
    {
    }
}

