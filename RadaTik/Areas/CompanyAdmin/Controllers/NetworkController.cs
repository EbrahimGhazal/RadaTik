using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPreview;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class NetworkController : global::RadaTik.Controllers.NetworkController
{
    public NetworkController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<global::RadaTik.Controllers.NetworkController> logger,
        IWebHostEnvironment environment,
        IUsageBasedSubscriptionChargeService usageSubscriptionChargeService,
        ICreatePricingPreviewService pricingPreviewService,
        ICompanyWalletOnboardingFundingService fundingService)
        : base(context, userManager, logger, environment, usageSubscriptionChargeService, pricingPreviewService, fundingService)
    {
    }
}

