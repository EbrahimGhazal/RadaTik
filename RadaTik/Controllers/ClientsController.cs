using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using RadaTik.Constants;

using RadaTik.Data;

using RadaTik.Models;

using RadaTik.Services.Clients;

using RadaTik.Security;



namespace RadaTik.Controllers

{

    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee,Client")]

    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Clients)]

    public partial class ClientsController : Controller

    {

        private sealed record ProfileOptionJson(int id, string? name);

        private sealed record ReceiverByServerJson(int id, string? name, string? sectorName);



        private readonly ApplicationDbContext _context;

        private readonly IClientApplicationServices _app;

        private readonly UserManager<ApplicationUser> _userManager;

        private readonly ILogger<ClientsController> _logger;



        public ClientsController(

            ApplicationDbContext context,

            IClientApplicationServices app,

            UserManager<ApplicationUser> userManager,

            ILogger<ClientsController> logger)

        {

            _context = context;

            _app = app;

            _userManager = userManager;

            _logger = logger;

        }

    }

}


