# -*- coding: utf-8 -*-
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "RadaTik" / "Controllers"
path = root / "ClientsController.cs"
lines = path.read_text(encoding="utf-8").splitlines(keepends=True)

usings = (
    "using Microsoft.AspNetCore.Authorization;\n"
    "using Microsoft.AspNetCore.Identity;\n"
    "using Microsoft.AspNetCore.Mvc;\n"
    "using Microsoft.AspNetCore.Mvc.Rendering;\n"
    "using Microsoft.EntityFrameworkCore;\n"
    "using RadaTik.Constants;\n"
    "using RadaTik.Data;\n"
    "using RadaTik.Models;\n"
    "using RadaTik.Services;\n"
    "using RadaTik.Services.PricingPolicies;\n"
    "using RadaTik.Services.PricingPreview;\n"
    "using RadaTik.Helpers;\n"
    "using RadaTik.Security;\n"
    "using System.Threading.Tasks;\n"
    "using System.Text.Json;\n"
    "using System.Text.RegularExpressions;\n"
    "using Microsoft.EntityFrameworkCore.Storage;\n\n"
)

core = usings + """namespace RadaTik.Controllers
{
    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee,Client")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Clients)]
    public partial class ClientsController : Controller
    {
        private sealed record ProfileOptionJson(int id, string? name);
        private sealed record ReceiverByServerJson(int id, string? name, string? sectorName);

        private const string ContractTemplateServiceKey = "CONTRACT_TEMPLATE";
        private const string ContractMetaServiceKey = "CONTRACT_META";

        private readonly ApplicationDbContext _context;
        private readonly IMikroTikUsersService _mikroTikService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ClientsController> _logger;
        private readonly PermissionService _permissionService;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ICreatePricingPreviewService _pricingPreviewService;
        private readonly RequestNotificationService _requestNotificationService;
        private readonly IClientRenewalGuardService _clientRenewalGuardService;
        private readonly ISubscriberInstallationInvoiceService _subscriberInstallationInvoiceService;
        private readonly ClientOperationsHubService _clientOperationsHubService;

""" + "".join(lines[33:41]) + """
        public ClientsController(
            ApplicationDbContext context,
            IMikroTikUsersService mikroTikService,
            UserManager<ApplicationUser> userManager,
            ILogger<ClientsController> logger,
            PermissionService permissionService,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ICreatePricingPreviewService pricingPreviewService,
            RequestNotificationService requestNotificationService,
            IClientRenewalGuardService clientRenewalGuardService,
            ISubscriberInstallationInvoiceService subscriberInstallationInvoiceService,
            ClientOperationsHubService clientOperationsHubService)
        {
            _context = context;
            _mikroTikService = mikroTikService;
            _userManager = userManager;
            _logger = logger;
            _permissionService = permissionService;
            _usageChargeService = usageChargeService;
            _pricingPreviewService = pricingPreviewService;
            _requestNotificationService = requestNotificationService;
            _clientRenewalGuardService = clientRenewalGuardService;
            _subscriberInstallationInvoiceService = subscriberInstallationInvoiceService;
            _clientOperationsHubService = clientOperationsHubService;
        }
    }
}
"""

wrap_open = usings + "namespace RadaTik.Controllers\n{\n    public partial class ClientsController : Controller\n    {\n"
wrap_close = "    }\n}\n"

splits = [
    ("ListAndContract", 63, 666),
    ("Crud", 668, 1362),
    ("RenewalAndSync", 1363, 1805),
    ("ImportAndApi", 1806, 2250),
]

path.write_text(core, encoding="utf-8")
for name, start, end in splits:
    body = "".join(lines[start - 1 : end])
    (root / f"ClientsController.{name}.cs").write_text(wrap_open + body + wrap_close, encoding="utf-8")
    print(name, end - start + 1, "lines")

print("core", len(core.splitlines()), "lines")
