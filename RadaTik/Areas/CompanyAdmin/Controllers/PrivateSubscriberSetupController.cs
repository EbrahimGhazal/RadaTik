using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// مسار دراسة: مشترك جديد + لاقط خاص (مواد كاملة + تركيب).
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class PrivateSubscriberSetupController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly PrivateSubscriberSetupOrchestrator _orchestrator;

    public PrivateSubscriberSetupController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        PrivateSubscriberSetupOrchestrator orchestrator)
    {
        _context = context;
        _userManager = userManager;
        _orchestrator = orchestrator;
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "تركيب مشترك — لاقط خاص";
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        await LoadFormListsAsync(networkId.Value, null);
        return View(new Client
        {
            IsActive = true,
            ServiceStartDate = DateTime.Today,
            AccountExpirationDate = DateTime.Today.AddMonths(1)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Name,UserName,Password,ProfileId,PhoneNumber,ResidenceAddress,ReceiverId,MikroTikServerId,IsActive")] Client client)
    {
        ViewData["Title"] = "تركيب مشترك — لاقط خاص";
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (!client.ReceiverId.HasValue)
        {
            ModelState.AddModelError(nameof(client.ReceiverId), "المستقبل (مكان اللاقط) مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(client.UserName))
        {
            ModelState.AddModelError(nameof(client.UserName), "اسم المستخدم مطلوب.");
        }

        if (client.ProfileId <= 0)
        {
            ModelState.AddModelError(nameof(client.ProfileId), "الباقة مطلوبة.");
        }

        if (client.MikroTikServerId.HasValue && client.ProfileId > 0)
        {
            bool profileBelongsToServer = await _context.Profiles
                .AsNoTracking()
                .AnyAsync(p =>
                    p.Id == client.ProfileId &&
                    p.NetworkId == networkId.Value &&
                    p.IsActive &&
                    p.MikroTikServerId == client.MikroTikServerId.Value);
            if (!profileBelongsToServer)
            {
                ModelState.AddModelError(nameof(client.ProfileId), "الباقة المختارة لا تتبع السيرفر المحدد.");
            }
        }

        if (!ModelState.IsValid)
        {
            await LoadFormListsAsync(networkId.Value, client.MikroTikServerId);
            return View(client);
        }

        ApplicationUser? existing = await _userManager.FindByNameAsync(client.UserName!);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(client.UserName), "اسم المستخدم موجود مسبقاً.");
            await LoadFormListsAsync(networkId.Value, client.MikroTikServerId);
            return View(client);
        }

        PrivateSubscriberSetupOrchestrator.CreateResult result = await _orchestrator.CreatePrivateSubscriberAsync(client, user, networkId.Value);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            await LoadFormListsAsync(networkId.Value, client.MikroTikServerId);
            return View(client);
        }

        TempData["Success"] = "تم إنشاء المشترك على MikroTik وفاتورة التركيب (مسودة). أكمل التثبيت النهائي ثم التحصيل.";
        return RedirectToAction("Details", "SubscriberInstallationInvoices", new { area = "CompanyAdmin", id = result.InvoiceId });
    }

    private async Task LoadFormListsAsync(int networkId, int? selectedServerId)
    {
        ViewBag.Receivers = new SelectList(
            await _context.Receivers.AsNoTracking()
                .Where(r => r.NetworkId == networkId && r.IsActive)
                .OrderBy(r => r.Name)
                .Select(r => new { r.Id, r.Name })
                .ToListAsync(),
            "Id",
            "Name");

        ViewBag.Profiles = new SelectList(
            await _context.Profiles.AsNoTracking()
                .Where(p =>
                    p.NetworkId == networkId &&
                    p.IsActive &&
                    selectedServerId.HasValue &&
                    p.MikroTikServerId == selectedServerId.Value)
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Name })
                .ToListAsync(),
            "Id",
            "Name");

        ViewBag.MikroTikServers = new SelectList(
            await _context.MikroTikServers.AsNoTracking()
                .Where(s => s.NetworkId == networkId && s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync(),
            "Id",
            "Name");
    }
}
