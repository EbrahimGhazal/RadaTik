using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.MaintenancePricing;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class RequestsManagementController : global::RadaTik.Controllers.RequestsManagementController
{
    public RequestsManagementController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikUsersService mikroTikService,
        IPermissionService permissionService,
        IMaintenanceBillingService maintenanceBillingService,
        IMaintenancePricingService maintenancePricingService,
        ILogger<global::RadaTik.Controllers.RequestsManagementController> logger)
        : base(context, userManager, mikroTikService, permissionService, maintenanceBillingService, maintenancePricingService, logger)
    {
    }
}

