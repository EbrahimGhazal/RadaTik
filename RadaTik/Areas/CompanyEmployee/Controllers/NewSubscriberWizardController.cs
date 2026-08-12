using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
public class NewSubscriberWizardController : CompanyAdmin.Controllers.NewSubscriberWizardController
{
    public NewSubscriberWizardController(
        ApplicationDbContext context,
        UserManager<Models.ApplicationUser> userManager,
        Services.NewSubscriberWizard.NewSubscriberWizardOrchestrator orchestrator,
        ISubscriberInstallationInvoiceService invoiceService,
        SubscriberInstallationWarehouseLinkService warehouseLinkService)
        : base(context, userManager, orchestrator, invoiceService, warehouseLinkService)
    {
    }
}
