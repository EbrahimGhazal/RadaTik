using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;

namespace RadTik.ViewComponents;

public class ClientPortalNetworkBrandModel
{
    public string? NetworkName { get; set; }
    public string? LogoPath { get; set; }
    public bool HasLogo => !string.IsNullOrWhiteSpace(LogoPath);
}

/// <summary>
/// شعار الشبكة التابع لها المشترك في رأس الشريط الجانلي لبوابة العميل.
/// </summary>
public class ClientPortalNetworkBrandViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClientPortalNetworkBrandViewComponent(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return Content(string.Empty);

        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user?.ClientId == null)
            return Content(string.Empty);

        var client = await _db.Clients
            .AsNoTracking()
            .Include(c => c.Network)
            .Include(c => c.MikroTikServer)
            .FirstOrDefaultAsync(c => c.Id == user.ClientId.Value);

        if (client == null)
            return Content(string.Empty);

        Network? net = client.Network;
        if (net == null && client.MikroTikServer?.NetworkId is int nid)
        {
            net = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == nid);
        }

        if (net == null)
            return View(new ClientPortalNetworkBrandModel());

        return View(new ClientPortalNetworkBrandModel
        {
            NetworkName = net.Name,
            LogoPath = net.LogoPath
        });
    }
}
