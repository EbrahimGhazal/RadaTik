using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using global::RadaTik.Data;

using global::RadaTik.Models;

using global::RadaTik.Helpers;

using global::RadaTik.Security;

using global::RadaTik.Services.Clients;



namespace RadaTik.Areas.CompanyEmployee.Controllers;



[Area("CompanyEmployee")]

[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]

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

    public new IActionResult Create() =>

        RedirectToAction("Index", "NewSubscriberWizard", new { area = "CompanyEmployee" });



    [HttpPost]

    [ValidateAntiForgeryToken]

    [RequirePermission("Clients.Create")]

    public new Task<IActionResult> Create(

        [Bind("Id,Name,SID,UserName,Password,ProfileId,PhoneNumber,ResidenceAddress,Latitude,Longitude,PowerSource,Building,Floor,IsActive,ReceiverId,Service,Address,MikroTikServerId,ServiceStartDate,AccountExpirationDate")]

        Client client,

        string? dbUserName,

        string? dbPassword) =>

        base.Create(client, dbUserName, dbPassword);

}


