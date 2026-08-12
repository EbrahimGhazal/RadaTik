using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPreview;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
public class ReceiverController : global::RadaTik.Controllers.ReceiverController
{
    public ReceiverController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ICreatePricingPreviewService pricingPreviewService,
        ILineOfSightAnalysisService lineOfSightAnalysisService)
        : base(context, userManager, usageChargeService, pricingPreviewService, lineOfSightAnalysisService)
    {
    }
}

