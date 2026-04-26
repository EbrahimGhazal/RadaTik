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
public class RequestsManagementController : RadTik.Controllers.RequestsManagementController
{
    public RequestsManagementController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikUsersService mikroTikService,
        PermissionService permissionService,
        IMaintenanceBillingService maintenanceBillingService,
        ILogger<RadTik.Controllers.RequestsManagementController> logger)
        : base(context, userManager, mikroTikService, permissionService, maintenanceBillingService, logger)
    {
    }
}

