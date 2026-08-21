using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyEmployee.Controllers
{
    /// <summary>
    /// CompanyEmployee Area wrapper around existing requests management logic.
    /// Exposes the same controller under /employee/RequestsManagement/*.
    /// </summary>
    [Area("CompanyEmployee")]
    // IMPORTANT: This Area must remain isolated to employees only.
    // Company admins (NetworkAdministrator) should use the CompanyAdmin area routes instead.
    [Authorize(Roles = $"{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
    public class RequestsManagementController : global::RadaTik.Controllers.RequestsManagementController
    {
        public RequestsManagementController(
            global::RadaTik.Data.ApplicationDbContext context,
            Microsoft.AspNetCore.Identity.UserManager<global::RadaTik.Models.ApplicationUser> userManager,
            global::RadaTik.Services.IMikroTikUsersService mikroTikService,
            global::RadaTik.Services.IPermissionService permissionService,
            global::RadaTik.Services.IMaintenanceBillingService maintenanceBillingService,
            global::RadaTik.Services.MaintenancePricing.IMaintenancePricingService maintenancePricingService,
            global::RadaTik.Services.IMaintenanceEmployeeTaskService maintenanceEmployeeTasks,
            ILogger<global::RadaTik.Controllers.RequestsManagementController> logger)
            : base(context, userManager, mikroTikService, permissionService, maintenanceBillingService, maintenancePricingService, maintenanceEmployeeTasks, logger)
        {
        }
    }
}

