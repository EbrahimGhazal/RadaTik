using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyEmployee.Controllers;

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
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
        }

        return RedirectToAction("Index", "Dashboard", new { area = "CompanyEmployee" });
    }
}

