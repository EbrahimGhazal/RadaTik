using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Erp)]
public class ErpCustomersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ErpCustomersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Title"] = "عملاء ERP";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        IQueryable<ErpCustomer> query = _context.ErpCustomers.AsNoTracking()
            .Where(c => c.CompanyNetworkId == scope.CompanyNetworkId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            string term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || (c.Phone != null && c.Phone.Contains(term)));
        }

        ViewBag.Query = q;
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(await query.OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "إضافة عميل ERP";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        await PopulateClientsAsync(scope.CompanyNetworkId);
        return View(new ErpCustomer { CompanyNetworkId = scope.CompanyNetworkId, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ErpCustomer model)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        model.CompanyNetworkId = scope.CompanyNetworkId;
        model.Name = model.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم العميل مطلوب.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateClientsAsync(scope.CompanyNetworkId);
            return View(model);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        model.CreatedByUserId = user?.Id;
        model.CreatedAt = DateTime.UtcNow;
        _context.ErpCustomers.Add(model);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم إضافة العميل.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "تعديل عميل ERP";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        ErpCustomer? customer = await _context.ErpCustomers
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyNetworkId == scope.CompanyNetworkId);
        if (customer == null)
        {
            return NotFound();
        }

        await PopulateClientsAsync(scope.CompanyNetworkId);
        return View(customer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ErpCustomer model)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        ErpCustomer? customer = await _context.ErpCustomers
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyNetworkId == scope.CompanyNetworkId);
        if (customer == null)
        {
            return NotFound();
        }

        customer.Name = model.Name?.Trim() ?? string.Empty;
        customer.Phone = model.Phone?.Trim();
        customer.Email = model.Email?.Trim();
        customer.Address = model.Address?.Trim();
        customer.Notes = model.Notes?.Trim();
        customer.ClientId = model.ClientId;
        customer.IsActive = model.IsActive;
        customer.UpdatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(customer.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم العميل مطلوب.");
            await PopulateClientsAsync(scope.CompanyNetworkId);
            return View(model);
        }

        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حفظ التعديلات.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<CompanyBusinessScopeHelper.CompanyScope?> ResolveScopeAsync()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
        }

        return scope;
    }

    private async Task PopulateClientsAsync(int companyNetworkId)
    {
        List<int> networkIds = await _context.Networks.AsNoTracking()
            .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
            .Select(n => n.Id)
            .ToListAsync();

        ViewBag.Clients = await _context.Clients.AsNoTracking()
            .Where(c => c.NetworkId != null && networkIds.Contains(c.NetworkId.Value))
            .OrderBy(c => c.UserName)
            .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.UserName ?? c.Id.ToString() })
            .ToListAsync();
    }
}
