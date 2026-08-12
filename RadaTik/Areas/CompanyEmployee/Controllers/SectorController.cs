using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPolicies;
using global::RadaTik.Services.PricingPreview;
using global::RadaTik.Services.MikroTik;
using global::RadaTik.Services.SectorRadio;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
public class SectorController : global::RadaTik.Controllers.SectorController
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public SectorController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ICreatePricingPreviewService pricingPreviewService,
        ISenderPricingOrchestrator senderPricingOrchestrator,
        IMikroTikSectorService mikroTikSectorService,
        ISectorRadioMetricsQueue sectorRadioQueue,
        ILineOfSightAnalysisService lineOfSight)
        : base(context, userManager, usageChargeService, pricingPreviewService, senderPricingOrchestrator, mikroTikSectorService, sectorRadioQueue, lineOfSight)
    {
        _context = context;
        _userManager = userManager;
    }

    [RequirePermission("Sectors.View")]
    public override async Task<IActionResult> Index()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = Helpers.NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        // نسخة خفيفة لواجهة الموظف: نتجنب استدعاءات MikroTik preview الثقيلة عند فتح التبويب.
        List<Sector> sectors = await _context.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId.Value)
            .Include(s => s.MikroTikServer)
            .Include(s => s.Receivers)
            .ToListAsync();

        Dictionary<int, int> userCountBySectorId = await _context.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId == networkId.Value && c.ReceiverId.HasValue)
            .Join(
                _context.Receivers.AsNoTracking(),
                c => c.ReceiverId!.Value,
                r => r.Id,
                (c, r) => new { r.SectorId })
            .GroupBy(x => x.SectorId)
            .Select(g => new { SectorId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SectorId, x => x.Count);

        ViewBag.UserCountBySectorId = userCountBySectorId;
        ViewBag.SectorImportServers = new List<MikroTikServer>();
        ViewBag.ImportPreviewByServer = new Dictionary<int, ImportSectorsPreviewResult>();
        ViewBag.ImportChargeByServer = new Dictionary<int, UsageImportChargeEstimate>();
        ViewBag.SectorImportUnitPrice = 0m;

        return View(sectors);
    }
}

