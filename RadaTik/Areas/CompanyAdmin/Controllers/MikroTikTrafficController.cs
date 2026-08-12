using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Dtos.Traffic;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// صفحة MVC لمراقبة ترافك واجهات MikroTik (SignalR). مدير الشركة فقط — تحت /networkManager/MikroTikTraffic.
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class MikroTikTrafficController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MikroTikTrafficController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? serverId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user?.NetworkId is not int mainNetId)
        {
            TempData["Error"] = "لا تتوفر شبكة مرتبطة بالحساب.";
            return RedirectToRoute("networkManager-dashboard");
        }

        var childIds = await _context.Networks.AsNoTracking()
            .Where(n => n.ParentNetworkId == mainNetId)
            .Select(n => n.Id)
            .ToListAsync();

        var networkIds = new List<int> { mainNetId };
        networkIds.AddRange(childIds);

        var servers = await _context.MikroTikServers.AsNoTracking()
            .Where(s => s.IsActive && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .OrderBy(s => s.Name)
            .Select(s => new ManagerMikroTikServerOptionDto
            {
                Id = s.Id,
                Name = s.Name,
                Host = s.Host,
                NetworkId = s.NetworkId!.Value,
            })
            .ToListAsync();

        int? initial = null;
        if (serverId.HasValue && servers.Any(x => x.Id == serverId.Value))
        {
            initial = serverId.Value;
        }

        ViewBag.ServersJson = JsonSerializer.Serialize(
            servers,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        ViewBag.InitialServerId = initial;

        return View("~/Areas/CompanyAdmin/Views/MikroTikTraffic/Index.cshtml", servers);
    }
}
