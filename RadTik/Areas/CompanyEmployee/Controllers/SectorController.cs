using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.PricingPolicies;
using RadTik.Services.SectorRadio;

namespace RadTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
public class SectorController : RadTik.Controllers.SectorController
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public SectorController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ISenderPricingOrchestrator senderPricingOrchestrator,
        IMikroTikUsersService mikroTikService,
        ISectorRadioMetricsQueue sectorRadioQueue)
        : base(context, userManager, usageChargeService, senderPricingOrchestrator, mikroTikService, sectorRadioQueue)
    {
        _context = context;
        _userManager = userManager;
    }

    [RequirePermission("Sectors.View")]
    public override async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var networkId = Helpers.NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network");
        }

        // نسخة خفيفة لواجهة الموظف: نتجنب استدعاءات MikroTik preview الثقيلة عند فتح التبويب.
        var sectors = await _context.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId == networkId.Value)
            .Include(s => s.MikroTikServer)
            .Include(s => s.Receivers)
            .ToListAsync();

        var userCountBySectorId = await _context.Clients
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

