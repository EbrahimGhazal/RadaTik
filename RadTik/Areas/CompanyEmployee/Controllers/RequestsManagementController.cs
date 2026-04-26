using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadTik.Security;

namespace RadTik.Areas.CompanyEmployee.Controllers
{
    /// <summary>
    /// CompanyEmployee Area wrapper around existing requests management logic.
    /// Exposes the same controller under /employee/RequestsManagement/*.
    /// </summary>
    [Area("CompanyEmployee")]
    // IMPORTANT: This Area must remain isolated to employees only.
    // Company admins (NetworkAdministrator) should use the CompanyAdmin area routes instead.
    [Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
    public class RequestsManagementController : global::RadTik.Controllers.RequestsManagementController
    {
        public RequestsManagementController(
            RadTik.Data.ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<RadTik.Models.ApplicationUser> userManager,
            RadTik.Services.IMikroTikUsersService mikroTikService,
            RadTik.Services.PermissionService permissionService,
            RadTik.Services.IMaintenanceBillingService maintenanceBillingService,
            ILogger<global::RadTik.Controllers.RequestsManagementController> logger)
            : base(context, userManager, mikroTikService, permissionService, maintenanceBillingService, logger)
        {
        }
    }
}

