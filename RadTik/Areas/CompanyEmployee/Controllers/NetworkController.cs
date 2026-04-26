using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.CompanyEmployee.Controllers;

[Area("CompanyEmployee")]
[Authorize(Roles = RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
public sealed class NetworkController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public NetworkController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // This controller exists only to handle legacy redirects like RedirectToAction("Index","Network")
    // from inherited controllers inside CompanyEmployee area when current network is not set.
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

        if (!networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
        }

        return RedirectToAction("Index", "Dashboard", new { area = "CompanyEmployee" });
    }
}

