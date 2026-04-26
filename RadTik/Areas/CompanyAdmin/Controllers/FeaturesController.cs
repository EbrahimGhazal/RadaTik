using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.CompanyAdmin;
using System.Net;

namespace RadTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// Company services hub in trial mode (all services free).
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class FeaturesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public FeaturesController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "الخدمات المتاحة (المرحلة التجريبية)";
        ViewData["BodyClass"] = "company-dashboard-page manager-dashboard-page features-hub-page";

        var user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل إضافة الخدمات.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        // Entitlements are managed at the main company network (ParentNetworkId == null).
        var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var effectiveNetwork = (selectedNetwork.ParentNetworkId.HasValue)
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        var vm = new CompanyServicesIndexViewModel
        {
            SelectedNetworkId = selectedNetwork.Id,
            SelectedNetworkName = selectedNetwork.Name,
            EffectiveCompanyNetworkId = effectiveNetworkId,
            EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name,
            CompanyBalance = effectiveNetwork?.Balance ?? 0m
        };

        var publicInfoByKey = await _context.FeaturePublicInfos
            .AsNoTracking()
            .ToDictionaryAsync(f => f.FeatureKey, f => f, StringComparer.OrdinalIgnoreCase);

        // Add custom services from SystemAdmin catalog
        var customServices = await _context.SystemServices
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayName)
            .ToListAsync();

        var allServiceDefs = FeatureCatalog.All
            .OrderBy(f => f.Category)
            .ThenBy(f => f.DisplayName)
            .Select(def => new { Key = def.Key, DisplayName = def.DisplayName, Category = def.Category, Description = def.Description })
            .Concat(customServices.Select(s => new { Key = s.Key, DisplayName = s.DisplayName, Category = "خدمات مخصصة", Description = s.Description ?? "" }))
            .ToList();

        var visibleServiceKeys = new HashSet<string>(
            allServiceDefs.Select(d => d.Key),
            StringComparer.OrdinalIgnoreCase);

        var servicesList = new List<CompanyServiceItemViewModel>();
        foreach (var def in allServiceDefs
                     .Where(d => visibleServiceKeys.Contains(d.Key))
                     .OrderBy(d => d.Category)
                     .ThenBy(d => d.DisplayName))
        {
            publicInfoByKey.TryGetValue(def.Key, out var pubInfo);
            var detailHtml = !string.IsNullOrWhiteSpace(pubInfo?.DetailHtml)
                ? pubInfo!.DetailHtml!
                : BuildTrialDefaultDetailHtml(def.DisplayName, def.Description);
            var pricingPolicyHtml = !string.IsNullOrWhiteSpace(pubInfo?.PricingPolicyHtml)
                ? pubInfo!.PricingPolicyHtml!
                : "<p class=\"text-success mb-0\">هذه الخدمة مفعّلة مجاناً بالكامل في المرحلة التجريبية، ولا تتطلب اشتراكاً أو طلب موافقة.</p>";

            servicesList.Add(new CompanyServiceItemViewModel
            {
                FeatureKey = def.Key,
                DisplayName = def.DisplayName,
                Category = def.Category,
                Description = def.Description ?? "",
                DetailHtml = detailHtml,
                PricingPolicyHtml = pricingPolicyHtml,
                HasActiveSubscription = true,
                HasPendingRequest = false,
                PricingOptions = []
            });
        }

        vm.Services = servicesList;

        return View(vm);
    }

    private static string BuildTrialDefaultDetailHtml(string displayName, string? description)
    {
        var safeName = WebUtility.HtmlEncode(displayName ?? "الخدمة");
        var safeDescription = WebUtility.HtmlEncode((description ?? string.Empty).Trim());
        var descriptionHtml = string.IsNullOrWhiteSpace(safeDescription)
            ? "هذه الخدمة متاحة الآن ضمن المرحلة التجريبية المجانية."
            : safeDescription;

        return $"""
                <p><strong>{safeName}</strong></p>
                <p>{descriptionHtml}</p>
                <p class="text-muted mb-0">يمكن لمدير الشركة تفعيل صلاحيات الموظفين لهذه الخدمة من شاشة إدارة الفريق.</p>
                """;
    }

}

