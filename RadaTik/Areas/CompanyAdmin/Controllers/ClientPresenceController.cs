using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Company;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class ClientPresenceController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ICompanyClientPresenceService _presence;

    public ClientPresenceController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ICompanyClientPresenceService presence)
    {
        _db = db;
        _userManager = userManager;
        _presence = presence;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? tab)
    {
        ViewData["Title"] = "حضور الشركة للمشترك";
        int? selectedId = await CurrentNetworkIdAsync();
        if (!selectedId.HasValue)
        {
            return NeedNetwork();
        }

        CompanyClientPresenceAdminPage? page = await _presence.GetAdminPageAsync(selectedId.Value, tab);
        return page == null ? NotFound() : View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddSocial(SocialMediaPlatform platform, string? displayName, string? url, bool isVisibleToClients = false) =>
        RunAsync(
            selected => _presence.AddSocialAsync(selected, new CompanySocialLinkSaveCommand
            {
                Platform = platform,
                DisplayName = displayName,
                Url = url,
                IsVisibleToClients = isVisibleToClients
            }),
            "social");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateSocial(int id, SocialMediaPlatform platform, string? displayName, string? url, bool isVisibleToClients) =>
        RunAsync(
            selected => _presence.UpdateSocialAsync(selected, id, new CompanySocialLinkSaveCommand
            {
                Platform = platform,
                DisplayName = displayName,
                Url = url,
                IsVisibleToClients = isVisibleToClients
            }),
            "social");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ToggleSocial(int id) =>
        RunAsync(selected => _presence.ToggleSocialAsync(selected, id), "social");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteSocial(int id) =>
        RunAsync(selected => _presence.DeleteSocialAsync(selected, id), "social");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddComplaint(string? label, string? phoneNumber, bool isVisibleToClients = false) =>
        RunAsync(
            selected => _presence.AddComplaintAsync(selected, new CompanyComplaintContactSaveCommand
            {
                Label = label,
                PhoneNumber = phoneNumber,
                IsVisibleToClients = isVisibleToClients
            }),
            "complaints");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> UpdateComplaint(int id, string? label, string? phoneNumber, bool isVisibleToClients) =>
        RunAsync(
            selected => _presence.UpdateComplaintAsync(selected, id, new CompanyComplaintContactSaveCommand
            {
                Label = label,
                PhoneNumber = phoneNumber,
                IsVisibleToClients = isVisibleToClients
            }),
            "complaints");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ToggleComplaint(int id) =>
        RunAsync(selected => _presence.ToggleComplaintAsync(selected, id), "complaints");

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> DeleteComplaint(int id) =>
        RunAsync(selected => _presence.DeleteComplaintAsync(selected, id), "complaints");

    private async Task<IActionResult> RunAsync(Func<int, Task<(bool Ok, string Message)>> work, string tab)
    {
        int? selectedId = await CurrentNetworkIdAsync();
        if (!selectedId.HasValue)
        {
            return NeedNetwork();
        }

        (bool ok, string message) = await work(selectedId.Value);
        TempData[ok ? "Success" : "Error"] = message;
        return RedirectToRoute("networkManager-client-presence", new { action = "Index", tab });
    }

    private async Task<int?> CurrentNetworkIdAsync()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        return NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
    }

    private IActionResult NeedNetwork()
    {
        TempData["Error"] = "يرجى تحديد شبكة أولاً.";
        return RedirectToRoute("networkManager-network", new { action = "Index" });
    }
}
