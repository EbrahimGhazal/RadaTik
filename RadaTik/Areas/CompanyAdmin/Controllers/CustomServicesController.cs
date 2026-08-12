using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class CustomServicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CustomServicesController> _logger;

    public CustomServicesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, ILogger<CustomServicesController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string serviceKey)
    {
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return NotFound();
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;

        // Ensure service exists, is active, and priced by system admin
        SystemService? service = await _context.SystemServices.AsNoTracking().FirstOrDefaultAsync(s => s.Key == serviceKey && s.IsActive);
        if (service == null)
        {
            return NotFound();
        }

        if (!await HasActivePricingAsync(serviceKey))
        {
            return NotFound();
        }

        ViewData["Title"] = service.DisplayName;
        ViewBag.ServiceKey = serviceKey;
        ViewBag.Service = service;

        List<CustomServiceItem> items = await _context.CustomServiceItems
            .AsNoTracking()
            .Where(i => i.NetworkId == effectiveNetworkId && i.ServiceKey == serviceKey)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create(string serviceKey)
    {
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return NotFound();
        }

        SystemService? service = await _context.SystemServices.AsNoTracking().FirstOrDefaultAsync(s => s.Key == serviceKey && s.IsActive);
        if (service == null)
        {
            return NotFound();
        }

        if (!await HasActivePricingAsync(serviceKey))
        {
            return NotFound();
        }

        ViewData["Title"] = $"إضافة - {service.DisplayName}";
        ViewBag.ServiceKey = serviceKey;
        ViewBag.Service = service;
        return View(new CustomServiceItem { ServiceKey = serviceKey });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CustomServiceItem model)
    {
        string serviceKey = model.ServiceKey;
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            return NotFound();
        }

        SystemService? service = await _context.SystemServices.AsNoTracking().FirstOrDefaultAsync(s => s.Key == serviceKey && s.IsActive);
        if (service == null)
        {
            return NotFound();
        }

        if (!await HasActivePricingAsync(serviceKey))
        {
            return NotFound();
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"إضافة - {service.DisplayName}";
            ViewBag.ServiceKey = serviceKey;
            ViewBag.Service = service;
            return View(model);
        }

        try
        {
            model.NetworkId = effectiveNetworkId;
            model.CreatedAt = DateTime.Now;
            model.UpdatedAt = DateTime.Now;
            _context.CustomServiceItems.Add(model);
            await _context.SaveChangesAsync();
            TempData["Success"] = AppMessages.OperationSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to create custom service item.");
            TempData["Error"] = "تعذر الحفظ.";
        }

        return RedirectToAction(nameof(Index), new { serviceKey });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        CustomServiceItem? item = await _context.CustomServiceItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }

        SystemService? service = await _context.SystemServices.AsNoTracking().FirstOrDefaultAsync(s => s.Key == item.ServiceKey && s.IsActive);
        if (service == null)
        {
            return NotFound();
        }

        if (!await HasActivePricingAsync(item.ServiceKey))
        {
            return NotFound();
        }

        ViewData["Title"] = $"تعديل - {service.DisplayName}";
        ViewBag.ServiceKey = item.ServiceKey;
        ViewBag.Service = service;
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CustomServiceItem model)
    {
        CustomServiceItem? existing = await _context.CustomServiceItems.FindAsync(model.Id);
        if (existing == null)
        {
            return NotFound();
        }

        SystemService? service = await _context.SystemServices.AsNoTracking().FirstOrDefaultAsync(s => s.Key == existing.ServiceKey && s.IsActive);
        if (service == null)
        {
            return NotFound();
        }

        if (!await HasActivePricingAsync(existing.ServiceKey))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = $"تعديل - {service.DisplayName}";
            ViewBag.ServiceKey = existing.ServiceKey;
            ViewBag.Service = service;
            return View(model);
        }

        existing.Title = model.Title;
        existing.Body = model.Body;
        existing.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        TempData["Success"] = AppMessages.OperationSuccess;

        return RedirectToAction(nameof(Index), new { serviceKey = existing.ServiceKey });
    }

    /// <summary>خدمة مخصّصة تظهر لمدير الشبكة فقط بعد أن يضيف مدير النظام سعراً نشطاً لها.</summary>
    private Task<bool> HasActivePricingAsync(string featureKey) =>
        _context.FeaturePricings.AsNoTracking().AnyAsync(p => p.FeatureKey == featureKey && p.IsActive);
}

