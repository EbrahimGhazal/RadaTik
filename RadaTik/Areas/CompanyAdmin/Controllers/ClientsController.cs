using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using global::RadaTik.Data;

using global::RadaTik.Models;

using global::RadaTik.Helpers;

using global::RadaTik.Security;

using global::RadaTik.Services.Clients;



namespace RadaTik.Areas.CompanyAdmin.Controllers;



[Area("CompanyAdmin")]

[Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]

public class ClientsController : global::RadaTik.Controllers.ClientsController

{

    public ClientsController(

        ApplicationDbContext context,

        IClientApplicationServices clientApp,

        UserManager<ApplicationUser> userManager,

        ILogger<global::RadaTik.Controllers.ClientsController> logger)

        : base(context, clientApp, userManager, logger)

    {

    }



    [HttpGet]

    [RequirePermission("Clients.Create")]

    public Task<IActionResult> CreateNetworkManager() => Create();

}


