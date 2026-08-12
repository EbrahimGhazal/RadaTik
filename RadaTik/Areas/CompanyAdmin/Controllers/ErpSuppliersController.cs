using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
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
public class ErpSuppliersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ErpSuppliersController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        ViewData["Title"] = "موردو ERP";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        IQueryable<ErpSupplier> query = _context.ErpSuppliers.AsNoTracking()
            .Where(s => s.CompanyNetworkId == scope.CompanyNetworkId);

        if (!string.IsNullOrWhiteSpace(q))
        {
            string term = q.Trim();
            query = query.Where(s => s.Name.Contains(term) || (s.Phone != null && s.Phone.Contains(term)));
        }

        ViewBag.Query = q;
        ViewBag.CompanyName = scope.CompanyNetworkName;
        return View(await query.OrderByDescending(s => s.CreatedAt).Take(200).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewData["Title"] = "إضافة مورد";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        return View(new ErpSupplier { CompanyNetworkId = scope.CompanyNetworkId, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ErpSupplier model)
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
            ModelState.AddModelError(nameof(model.Name), "اسم المورد مطلوب.");
            return View(model);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        model.CreatedByUserId = user?.Id;
        model.CreatedAt = DateTime.UtcNow;
        _context.ErpSuppliers.Add(model);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم إضافة المورد.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["Title"] = "تعديل مورد";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        ErpSupplier? supplier = await _context.ErpSuppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyNetworkId == scope.CompanyNetworkId);
        return supplier == null ? NotFound() : View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ErpSupplier model)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        ErpSupplier? supplier = await _context.ErpSuppliers
            .FirstOrDefaultAsync(s => s.Id == id && s.CompanyNetworkId == scope.CompanyNetworkId);
        if (supplier == null)
        {
            return NotFound();
        }

        supplier.Name = model.Name?.Trim() ?? string.Empty;
        supplier.Phone = model.Phone?.Trim();
        supplier.Email = model.Email?.Trim();
        supplier.Address = model.Address?.Trim();
        supplier.Notes = model.Notes?.Trim();
        supplier.IsActive = model.IsActive;
        supplier.UpdatedAt = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(supplier.Name))
        {
            ModelState.AddModelError(nameof(model.Name), "اسم المورد مطلوب.");
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
}
