using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.ViewModels.Admin;

namespace RadTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// إدارة مستخدمي الشبكة (مدير الشركة فقط) - ضمن Area منظمة: /CompanyAdmin/Admin
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Users)]
public class AdminController : Controller
{
    // Views are located under: /Areas/CompanyAdmin/Views/Admin/*

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> logger,
        IUsageBasedSubscriptionChargeService usageChargeService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _usageChargeService = usageChargeService;
    }

    [HttpGet]
    public async Task<IActionResult> CreateEmployee(string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var vm = new CreateEmployeeViewModel
        {
            ReturnUrl = returnUrl
        };

        await LoadEmployeePermissionMatrixAsync(networkId.Value);
        await LoadEmployeeCreatePricingNoteAsync(networkId.Value);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (!ModelState.IsValid)
        {
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            await LoadEmployeeCreatePricingNoteAsync(networkId.Value);
            return View(model);
        }

        var selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

        var employeeChargeEstimate = await _usageChargeService.EstimateImportChargeAsync(
            companyNetworkId,
            PricingChargeUnit.PerUser,
            1);
        if (employeeChargeEstimate.HasCharge && !employeeChargeEstimate.HasSufficientBalance)
        {
            TempData["Error"] =
                $"لا يمكن إنشاء الموظف الآن: الرصيد الحالي ({employeeChargeEstimate.WalletBalance:N2}) أقل من المطلوب ({employeeChargeEstimate.RequiredAmountSyp:N2}) ل.س.ج.";
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            await LoadEmployeeCreatePricingNoteAsync(networkId.Value);
            return View(model);
        }

        var userName = (model.UserName ?? string.Empty).Trim();
        var email = (model.Email ?? string.Empty).Trim();

        var user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            CreatedDate = DateTime.Now,
            IsActive = model.IsActive,
            NetworkId = networkId.Value
        };

        var createResult = await _userManager.CreateAsync(user, model.Password ?? string.Empty);
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            await LoadEmployeeCreatePricingNoteAsync(networkId.Value);
            return View(model);
        }

        // الموظف التابع للشركة: CompanyEmployee
        var addRoleResult = await _userManager.AddToRoleAsync(user, RoleNames.CompanyEmployee);
        if (!addRoleResult.Succeeded)
        {
            // Rollback: لا نترك مستخدم بدون دور
            try { await _userManager.DeleteAsync(user); } catch { /* ignore */ }

            foreach (var error in addRoleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            await LoadEmployeeCreatePricingNoteAsync(networkId.Value);
            return View(model);
        }

        // حفظ الصلاحيات المختارة (Permissions) للموظف
        try
        {
            var selectedIds = (model.SelectedPermissionIds ?? [])
                .Distinct()
                .ToList();

            if (selectedIds.Count > 0)
            {
                var validIds = await GetAllowedPermissionIdsAsync(networkId.Value, selectedIds);

                if (validIds.Count > 0)
                {
                    _context.UserPermissions.AddRange(validIds.Select(pid => new UserPermission
                    {
                        UserId = user.Id,
                        PermissionId = pid,
                        CreatedAt = DateTime.Now
                    }));

                    await _context.SaveChangesAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر حفظ صلاحيات الموظف");
            TempData["Error"] = "تم إنشاء الموظف لكن تعذر حفظ الصلاحيات. تأكد من تطبيق الهجرات (migrations).";
        }

        TempData["Success"] = "تم إنشاء الموظف بنجاح";

        await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, currentUser.Id, PricingChargeUnit.PerUser);

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return RedirectToAction("Index", new { type = "employees" });
    }

    [HttpGet]
    public async Task<IActionResult> DetailsEmployee(string id, string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        // تفاصيل الموظف/المستخدم ضمن الشبكة الحالية
        var roles = await _userManager.GetRolesAsync(user);
        var vm = new DeleteEmployeeViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            ReturnUrl = returnUrl
        };

        ViewBag.Roles = roles;
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> EditEmployee(string id, string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        var selectedPermissionIds = new List<int>();
        try
        {
            selectedPermissionIds = await _context.UserPermissions
                .Where(up => up.UserId == user.Id)
                .Select(up => up.PermissionId)
                .ToListAsync();
        }
        catch
        {
            // ignore (migrations not applied)
        }

        var vm = new EditEmployeeViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            SelectedPermissionIds = selectedPermissionIds,
            ReturnUrl = returnUrl
        };

        await LoadEmployeePermissionMatrixAsync(networkId.Value);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEmployee(EditEmployeeViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (!ModelState.IsValid)
        {
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            return View(model);
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.Id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        // تحقق من تكرار اسم المستخدم
        var newUserName = (model.UserName ?? string.Empty).Trim();
        var existing = await _userManager.FindByNameAsync(newUserName);
        if (existing != null && existing.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم مستخدم مسبقاً");
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            return View(model);
        }

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            user.UserName = newUserName;
            user.Email = (model.Email ?? string.Empty).Trim();
            user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.IsActive = model.IsActive;
            user.LastUpdated = DateTime.Now;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var err in updateResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                await tx.RollbackAsync();
                await LoadEmployeePermissionMatrixAsync(networkId.Value);
                return View(model);
            }

            // تغيير كلمة المرور (اختياري)
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!resetResult.Succeeded)
                {
                    foreach (var err in resetResult.Errors)
                    {
                        ModelState.AddModelError(string.Empty, err.Description);
                    }
                    await tx.RollbackAsync();
                    await LoadEmployeePermissionMatrixAsync(networkId.Value);
                    return View(model);
                }
            }

            // تحديث الصلاحيات
            try
            {
                var selectedIds = (model.SelectedPermissionIds ?? [])
                    .Distinct()
                    .ToList();

                var existingPerms = _context.UserPermissions.Where(up => up.UserId == user.Id);
                _context.UserPermissions.RemoveRange(existingPerms);
                await _context.SaveChangesAsync();

                if (selectedIds.Count > 0)
                {
                    var validIds = await GetAllowedPermissionIdsAsync(networkId.Value, selectedIds);

                    if (validIds.Count > 0)
                    {
                        _context.UserPermissions.AddRange(validIds.Select(pid => new UserPermission
                        {
                            UserId = user.Id,
                            PermissionId = pid,
                            CreatedAt = DateTime.Now
                        }));
                        await _context.SaveChangesAsync();
                    }
                }
            }
            catch
            {
                TempData["Error"] = "تم تحديث بيانات المستخدم لكن تعذر حفظ/تحديث الصلاحيات. تأكد من تطبيق الهجرات (migrations).";
            }

            await tx.CommitAsync();

            TempData["Success"] = "تم تحديث بيانات المستخدم وصلاحياته بنجاح";
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", new { type = "employees" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء تعديل بيانات المستخدم");
            try { await tx.RollbackAsync(); } catch { }
            TempData["Error"] = "حدث خطأ أثناء حفظ التغييرات";
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleEmployeeStatus(string id, string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = !user.IsActive;
        user.LastUpdated = DateTime.Now;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = "تعذر تحديث حالة المستخدم";
        }
        else
        {
            TempData["Success"] = user.IsActive ? "تم تفعيل حساب المستخدم" : "تم تجميد حساب المستخدم";
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction("Index", new { type = "employees" });
    }

    [HttpGet]
    public async Task<IActionResult> DeleteEmployee(string id, string? returnUrl = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.CompanyEmployee) &&
            !await _userManager.IsInRoleAsync(user, RoleNames.EmployeeLegacy))
        {
            return Forbid();
        }

        var vm = new DeleteEmployeeViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            ReturnUrl = returnUrl
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEmployeeConfirmed(DeleteEmployeeViewModel model)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.Id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.CompanyEmployee) &&
            !await _userManager.IsInRoleAsync(user, RoleNames.EmployeeLegacy))
        {
            return Forbid();
        }

        using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // إزالة صلاحيات الموظف أولاً (لتفادي مشاكل FK إذا لم تكن Cascade)
            try
            {
                var perms = _context.UserPermissions.Where(up => up.UserId == user.Id);
                _context.UserPermissions.RemoveRange(perms);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // ignore
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                await tx.RollbackAsync();
                return View(model);
            }

            await tx.CommitAsync();

            TempData["Success"] = "تم حذف الموظف بنجاح";
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
            {
                return Redirect(model.ReturnUrl);
            }
            return RedirectToAction("Index", new { type = "employees" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطأ أثناء حذف الموظف");
            try { await tx.RollbackAsync(); } catch { }
            TempData["Error"] = "حدث خطأ أثناء حذف الموظف";
            return View(model);
        }
    }

    private async Task LoadEmployeePermissionMatrixAsync(int selectedNetworkId)
    {
        try
        {
            var enabledFeatureKeys = await GetEnabledFeatureKeysAsync(selectedNetworkId);
            var allPermissions = await _context.Permissions.AsNoTracking().ToListAsync();
            var rows = EmployeeServicePermissionMatrix.BuildRows(enabledFeatureKeys, allPermissions);
            ViewBag.EmployeeServicePermissionRows = rows;
        }
        catch
        {
            ViewBag.EmployeeServicePermissionRows = new List<EmployeeServicePermissionUiRow>();
        }
    }

    private async Task LoadEmployeeCreatePricingNoteAsync(int selectedNetworkId)
    {
        try
        {
            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            var estimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerUser,
                1);

            ViewBag.EmployeeCreateChargeHasPricing = estimate.HasCharge;
            ViewBag.EmployeeCreateChargeAmount = estimate.RequiredAmountSyp;
            ViewBag.EmployeeCreateChargeWalletBalance = estimate.WalletBalance;

            var pricingRows = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.Users &&
                    p.ChargeUnit == PricingChargeUnit.PerUser)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var initialPricing = pricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
            var renewalPricing = pricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);

            ViewBag.EmployeeCreateInitialPrice = initialPricing?.AmountSYP ?? estimate.RequiredAmountSyp;
            ViewBag.EmployeeCreateRenewalPrice = renewalPricing?.AmountSYP ?? 0m;
            ViewBag.EmployeeCreateRenewalPeriodLabel = renewalPricing != null
                ? PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod)
                : null;
            ViewBag.EmployeeCreateHasRenewalPricing = renewalPricing != null;
        }
        catch
        {
            ViewBag.EmployeeCreateChargeHasPricing = false;
            ViewBag.EmployeeCreateChargeAmount = 0m;
            ViewBag.EmployeeCreateChargeWalletBalance = 0m;
            ViewBag.EmployeeCreateInitialPrice = 0m;
            ViewBag.EmployeeCreateRenewalPrice = 0m;
            ViewBag.EmployeeCreateRenewalPeriodLabel = null;
            ViewBag.EmployeeCreateHasRenewalPricing = false;
        }
    }

    private async Task<HashSet<string>> GetEnabledFeatureKeysAsync(int selectedNetworkId)
    {
        var featureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FeatureKeys.Users // صفحة إدارة المستخدمين نفسها
        };

        // إتاحة كل خدمات مصفوفة صلاحيات الموظف دائماً لمدير الشركة
        // حتى لو لم تُسجل اشتراكاتها بعد في NetworkServiceSubscriptions.
        foreach (var supportedKey in EmployeeServicePermissionMatrix.GetSupportedFeatureKeys())
        {
            featureKeys.Add(supportedKey);
        }

        var effectiveNetworkId = selectedNetworkId;
        var selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
        if (selectedNetwork?.ParentNetworkId != null)
        {
            effectiveNetworkId = selectedNetwork.ParentNetworkId.Value;
        }

        var now = DateTime.Now;
        var subscribedFeatureKeys = await _context.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s =>
                s.NetworkId == effectiveNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Active &&
                s.ExpiresAt > now)
            .Select(s => s.FeatureKey)
            .ToListAsync();

        foreach (var key in subscribedFeatureKeys)
        {
            featureKeys.Add(key);
        }

        // دعم التوافق مع الجدول القديم (NetworkFeatures) إن وُجدت بيانات فيه
        var legacyEnabledFeatures = await _context.NetworkFeatures
            .AsNoTracking()
            .Where(f => f.NetworkId == effectiveNetworkId && f.IsEnabled)
            .Select(f => f.Key)
            .ToListAsync();

        foreach (var key in legacyEnabledFeatures)
        {
            featureKeys.Add(key);
        }

        return featureKeys;
    }

    private async Task<List<int>> GetAllowedPermissionIdsAsync(int selectedNetworkId, List<int> requestedIds)
    {
        if (requestedIds.Count == 0)
        {
            return [];
        }

        var enabledFeatureKeys = await GetEnabledFeatureKeysAsync(selectedNetworkId);
        var allPermissions = await _context.Permissions.AsNoTracking().ToListAsync();
        var allowed = EmployeeServicePermissionMatrix.GetAllowedPermissionIds(enabledFeatureKeys, allPermissions);

        return requestedIds
            .Distinct()
            .Where(id => allowed.Contains(id))
            .ToList();
    }

    /// <summary>
    /// صفحة المستخدمين داخل الشبكة الحالية مع إمكانية التصفية حسب النوع (مشتركين، نقاط بيع، موظفين)
    /// </summary>
    public async Task<IActionResult> Index(string? q = null, string? type = null)
    {
        var currentUser = await _userManager.GetUserAsync(User);
        var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        // إذا كان النوع هو "clients" نعرض صفحة خاصة بالمشتركين
        if (!string.IsNullOrWhiteSpace(type) &&
            type.Equals("clients", StringComparison.OrdinalIgnoreCase))
        {
            return await ClientsList(q, networkId.Value);
        }

        // جميع المستخدمين المرتبطين بهذه الشبكة (باستثناء مدير النظام)
        var usersQuery = _userManager.Users
            .Where(u => u.NetworkId == networkId.Value)
            .OrderBy(u => u.UserName)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            usersQuery = usersQuery.Where(u =>
                (u.UserName != null && u.UserName.Contains(q)) ||
                (u.FullName != null && u.FullName.Contains(q)) ||
                (u.Email != null && u.Email.Contains(q)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(q)));
        }

        var users = await usersQuery.ToListAsync();

        var model = new List<RadTik.Controllers.AdminUserListItem>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);

            model.Add(new RadTik.Controllers.AdminUserListItem
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                FullName = u.FullName ?? "",
                Email = u.Email ?? "",
                PhoneNumber = u.PhoneNumber ?? "",
                IsActive = u.IsActive,
                Roles = string.Join(", ", roles),
                CreatedDate = u.CreatedDate,
                LastUpdated = u.LastUpdated
            });
        }

        // تصفية حسب النوع المطلوب
        string? filterTitle = null;
        if (!string.IsNullOrWhiteSpace(type))
        {
            type = type.ToLowerInvariant();
            switch (type)
            {
                case "clients":
                    filterTitle = "المشتركين";
                    break;
                case "points":
                    filterTitle = "نقاط البيع";
                    break;
                case "employees":
                    filterTitle = "الموظفين";
                    break;
            }
        }

        if (string.Equals(type, "employees", StringComparison.OrdinalIgnoreCase))
        {
            model = model
                .Where(u =>
                {
                    var roles = u.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim());
                    return roles.Contains(RoleNames.CompanyEmployee) || roles.Contains(RoleNames.EmployeeLegacy);
                })
                .ToList();
        }
        else if (string.Equals(type, "clients", StringComparison.OrdinalIgnoreCase))
        {
            model = model
                .Where(u => u.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Contains(RoleNames.Client))
                .ToList();
        }
        else if (string.Equals(type, "points", StringComparison.OrdinalIgnoreCase))
        {
            model = model
                .Where(u => u.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Contains(RoleNames.CollectionPoint))
                .ToList();
        }

        ViewBag.Search = q;
        ViewBag.NetworkId = networkId.Value;
        ViewBag.Type = type;
        ViewBag.FilterTitle = filterTitle;

        return View(model);
    }

    /// <summary>
    /// عرض جميع المشتركين التابعين لمدير الشركة مع معلومات الشبكة والقطاع والسيرفر
    /// </summary>
    private async Task<IActionResult> ClientsList(string? q, int networkId)
    {
        var clientsQuery = _context.Clients
            .Where(c => c.NetworkId == networkId)
            .Include(c => c.Network)
            .Include(c => c.Receiver)
                .ThenInclude(r => r!.Sector)
            .Include(c => c.MikroTikServer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            clientsQuery = clientsQuery.Where(c =>
                (c.Name != null && c.Name.Contains(q)) ||
                (c.UserName != null && c.UserName.Contains(q)) ||
                (c.Receiver != null && c.Receiver.Name != null && c.Receiver.Name.Contains(q)) ||
                (c.Receiver != null && c.Receiver.Sector != null && c.Receiver.Sector.Name != null && c.Receiver.Sector.Name.Contains(q)) ||
                (c.MikroTikServer != null && c.MikroTikServer.Name != null && c.MikroTikServer.Name.Contains(q)));
        }

        var clients = await clientsQuery.ToListAsync();

        var model = clients.Select(c => new RadTik.Controllers.AdminClientListItem
        {
            Id = c.Id,
            NetworkName = c.Network?.Name ?? "",
            ClientName = c.Name ?? "",
            UserName = c.UserName ?? "",
            ReceiverName = c.Receiver?.Name ?? "",
            SectorName = c.Receiver?.Sector?.Name ?? "",
            CreatedDate = c.CreatedDate,
            ExpirationDate = c.AccountExpirationDate,
            LastUpdated = c.LastUpdated,
            ServerName = c.MikroTikServer?.Name ?? ""
        }).ToList();

        ViewBag.Search = q;
        ViewBag.NetworkId = networkId;
        ViewBag.Type = "clients";
        ViewBag.FilterTitle = "المشتركين";

        return View("Clients", model);
    }
}

