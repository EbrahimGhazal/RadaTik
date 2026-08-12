using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Models.Business;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPreview;
using global::RadaTik.ViewModels.Admin;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// إدارة مستخدمي الشبكة (مدير الشركة فقط) - ضمن Area منظمة: /CompanyAdmin/Admin
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Users)]
public class AdminController : Controller
{
    // Views are located under: /Areas/CompanyAdmin/Views/Admin/*

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AdminController> _logger;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
    private readonly ICreatePricingPreviewService _pricingPreviewService;
    private readonly CompanyHrIntegrationService _hrIntegrationService;

    public AdminController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<AdminController> logger,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ICreatePricingPreviewService pricingPreviewService,
        CompanyHrIntegrationService hrIntegrationService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _usageChargeService = usageChargeService;
        _pricingPreviewService = pricingPreviewService;
        _hrIntegrationService = hrIntegrationService;
    }

    [HttpGet]
    public async Task<IActionResult> CreateEmployee(string? returnUrl = null)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        CreateEmployeeViewModel vm = new CreateEmployeeViewModel
        {
            ReturnUrl = returnUrl
        };

        await LoadEmployeePermissionMatrixAsync(networkId.Value);
        await LoadEmployeeCopySourcesAsync(networkId.Value);
        await LoadEmployeeCreatePricingNoteAsync(networkId.Value);
        ViewBag.EmployeePasswordMinLength = StrongPasswordRules.MinimumLength;
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ValidateCreateEmployeeAccount(CreateEmployeeViewModel model)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
        if (currentUser == null || !networkId.HasValue)
        {
            return Json(new { isValid = false, fieldErrors = new Dictionary<string, string[]>(), generalErrors = new[] { AppMessages.SelectNetworkFirst } });
        }

        CreateEmployeeAccountValidationResult validation = await CreateEmployeeAccountValidator.ValidateAsync(_userManager, model);
        return Json(ToAccountValidationJson(validation));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEmployee(CreateEmployeeViewModel model)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        CreateEmployeeAccountValidationResult accountValidation = await CreateEmployeeAccountValidator.ValidateAsync(_userManager, model);
        ApplyAccountValidationToModelState(accountValidation);

        if (!ValidateEmployeeDepartmentSelection(model.Department, model.SelectedPermissionIds, nameof(model.Department)))
        {
            await ReloadCreateEmployeeViewAsync(networkId.Value, wizardStep: 2);
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            await ReloadCreateEmployeeViewAsync(networkId.Value, wizardStep: ResolveWizardStepFromModelState());
            return View(model);
        }

        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

        UsageImportChargeEstimate employeeChargeEstimate = await _usageChargeService.EstimateImportChargeAsync(
            companyNetworkId,
            PricingChargeUnit.PerUser,
            1);
        if (employeeChargeEstimate.HasCharge && !employeeChargeEstimate.HasSufficientBalance)
        {
            TempData["Error"] =
                $"لا يمكن إنشاء الموظف الآن: الرصيد الحالي ({employeeChargeEstimate.WalletBalance:N2}) أقل من المطلوب ({employeeChargeEstimate.RequiredAmountSyp:N2}) ل.س.ج.";
            await ReloadCreateEmployeeViewAsync(networkId.Value, wizardStep: 4);
            return View(model);
        }

        string userName = (model.UserName ?? string.Empty).Trim();
        string email = (model.Email ?? string.Empty).Trim();

        ApplicationUser user = new ApplicationUser
        {
            UserName = userName,
            Email = email,
            FullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim(),
            PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
            CreatedDate = DateTime.Now,
            IsActive = model.IsActive,
            NetworkId = networkId.Value,
            EmployeeDepartment = await ResolveEmployeeDepartmentForSaveAsync(
                model.Department,
                model.SelectedPermissionIds,
                networkId.Value)
        };

        IdentityResult createResult = await _userManager.CreateAsync(user, model.Password ?? string.Empty);
        if (!createResult.Succeeded)
        {
            foreach (IdentityError error in createResult.Errors)
            {
                MapIdentityErrorToModelState(error);
            }

            await ReloadCreateEmployeeViewAsync(networkId.Value, wizardStep: ResolveWizardStepFromModelState());
            return View(model);
        }

        // الموظف التابع للشركة: CompanyEmployee
        IdentityResult addRoleResult = await _userManager.AddToRoleAsync(user, RoleNames.CompanyEmployee);
        if (!addRoleResult.Succeeded)
        {
            // Rollback: لا نترك مستخدم بدون دور
            try { await _userManager.DeleteAsync(user); } catch { /* ignore */ }

            foreach (IdentityError error in addRoleResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            await ReloadCreateEmployeeViewAsync(networkId.Value, wizardStep: 4);
            return View(model);
        }

        // حفظ الصلاحيات المختارة (Permissions) للموظف
        try
        {
            List<int> selectedIds = (model.SelectedPermissionIds ?? [])
                .Distinct()
                .ToList();

            if (selectedIds.Count > 0)
            {
                List<int> validIds = await GetAllowedPermissionIdsAsync(networkId.Value, selectedIds);

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

        try
        {
            await _hrIntegrationService.EnsurePayrollRecordForUserAsync(
                user,
                companyNetworkId,
                model.SyncToPayroll ? model.MonthlySalary : null,
                model.SyncToPayroll ? model.PayrollEmploymentType : PayrollEmploymentType.FullTime,
                model.SyncToPayroll ? model.WeeklyWorkHours : null,
                model.SyncToPayroll ? model.PayrollJobTitle : null);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "تعذر ربط الموظف بسجل الرواتب");
            TempData["Info"] = "تم إنشاء الموظف لكن تعذر إنشاء سجل الرواتب تلقائياً — أضفه يدوياً من شاشة الرواتب.";
        }

        TempData["Success"] = AppMessages.OperationSuccess;

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
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        // تفاصيل الموظف/المستخدم ضمن الشبكة الحالية
        IList<string> roles = await _userManager.GetRolesAsync(user);
        DeleteEmployeeViewModel vm = new DeleteEmployeeViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            FullName = user.FullName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            ReturnUrl = returnUrl
        };

        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
        PayrollEmployee? payroll = await _hrIntegrationService.GetPayrollForUserAsync(companyNetworkId, user.Id);
        ViewBag.Roles = roles;
        ViewBag.PayrollEmployeeId = payroll?.Id;
        ViewBag.HasPayrollRecord = payroll != null;
        ViewBag.EmployeeDepartmentName = EmployeeDepartmentTemplates.GetDisplayName(user.EmployeeDepartment);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnsurePayrollForEmployee(string id, decimal? monthlySalary = null)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
        if (currentUser == null || !networkId.HasValue || string.IsNullOrWhiteSpace(id))
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", new { type = "employees" });
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

        try
        {
            await _hrIntegrationService.EnsurePayrollRecordForUserAsync(user, companyNetworkId, monthlySalary);
            TempData["Success"] = "تم إنشاء/ربط سجل الرواتب بنجاح.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "EnsurePayrollForEmployee failed for {UserId}", id);
            TempData["Error"] = "تعذر ربط سجل الرواتب.";
        }

        return RedirectToAction(nameof(DetailsEmployee), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> EditEmployee(string id, string? returnUrl = null)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return NotFound();
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        List<int> selectedPermissionIds = new List<int>();
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

        EditEmployeeViewModel vm = new EditEmployeeViewModel
        {
            Id = user.Id,
            UserName = user.UserName ?? "",
            Email = user.Email ?? "",
            FullName = user.FullName,
            PhoneNumber = user.PhoneNumber,
            IsActive = user.IsActive,
            Department = user.EmployeeDepartment,
            SelectedPermissionIds = selectedPermissionIds,
            ReturnUrl = returnUrl
        };

        if (vm.Department == EmployeeDepartment.None && selectedPermissionIds.Count > 0)
        {
            try
            {
                HashSet<string> enabledFeatureKeys = await GetEnabledFeatureKeysAsync(networkId.Value);
                List<Permission> allPermissions = await _context.Permissions.AsNoTracking().ToListAsync();
                vm.Department = EmployeeDepartmentTemplates.DetectDepartment(
                    selectedPermissionIds,
                    enabledFeatureKeys,
                    allPermissions);
            }
            catch
            {
                vm.Department = EmployeeDepartment.Custom;
            }
        }

        await LoadEmployeePermissionMatrixAsync(networkId.Value);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditEmployee(EditEmployeeViewModel model)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        if (!ModelState.IsValid)
        {
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            return View(model);
        }

        if (!ValidateEmployeeDepartmentSelection(model.Department, model.SelectedPermissionIds, nameof(model.Department)))
        {
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            return View(model);
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.Id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        // تحقق من تكرار اسم المستخدم
        string newUserName = (model.UserName ?? string.Empty).Trim();
        ApplicationUser? existing = await _userManager.FindByNameAsync(newUserName);
        if (existing != null && existing.Id != user.Id)
        {
            ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم مستخدم مسبقاً");
            await LoadEmployeePermissionMatrixAsync(networkId.Value);
            return View(model);
        }

        using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
        try
        {
            user.UserName = newUserName;
            user.Email = (model.Email ?? string.Empty).Trim();
            user.FullName = string.IsNullOrWhiteSpace(model.FullName) ? null : model.FullName.Trim();
            user.PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim();
            user.IsActive = model.IsActive;
            user.EmployeeDepartment = await ResolveEmployeeDepartmentForSaveAsync(
                model.Department,
                model.SelectedPermissionIds,
                networkId.Value);
            user.LastUpdated = DateTime.Now;

            IdentityResult updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (IdentityError err in updateResult.Errors)
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
                string token = await _userManager.GeneratePasswordResetTokenAsync(user);
                IdentityResult resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
                if (!resetResult.Succeeded)
                {
                    foreach (IdentityError err in resetResult.Errors)
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
                List<int> selectedIds = (model.SelectedPermissionIds ?? [])
                    .Distinct()
                    .ToList();

                IQueryable<UserPermission> existingPerms = _context.UserPermissions.Where(up => up.UserId == user.Id);
                _context.UserPermissions.RemoveRange(existingPerms);
                await _context.SaveChangesAsync();

                if (selectedIds.Count > 0)
                {
                    List<int> validIds = await GetAllowedPermissionIdsAsync(networkId.Value, selectedIds);

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

            TempData["Success"] = AppMessages.OperationSuccess;
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
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        user.IsActive = !user.IsActive;
        user.LastUpdated = DateTime.Now;
        IdentityResult result = await _userManager.UpdateAsync(user);
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
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.CompanyEmployee) &&
            !await _userManager.IsInRoleAsync(user, RoleNames.EmployeeLegacy))
        {
            return Forbid();
        }

        DeleteEmployeeViewModel vm = new DeleteEmployeeViewModel
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
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ApplicationUser? user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == model.Id && u.NetworkId == networkId.Value);
        if (user == null)
        {
            return NotFound();
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.CompanyEmployee) &&
            !await _userManager.IsInRoleAsync(user, RoleNames.EmployeeLegacy))
        {
            return Forbid();
        }

        using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // إزالة صلاحيات الموظف أولاً (لتفادي مشاكل FK إذا لم تكن Cascade)
            try
            {
                IQueryable<UserPermission> perms = _context.UserPermissions.Where(up => up.UserId == user.Id);
                _context.UserPermissions.RemoveRange(perms);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // ignore
            }

            IdentityResult result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                foreach (IdentityError err in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, err.Description);
                }
                await tx.RollbackAsync();
                return View(model);
            }

            await tx.CommitAsync();

            TempData["Success"] = AppMessages.OperationSuccess;
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

    private async Task ReloadCreateEmployeeViewAsync(int networkId, int wizardStep)
    {
        await LoadEmployeePermissionMatrixAsync(networkId);
        await LoadEmployeeCopySourcesAsync(networkId);
        await LoadEmployeeCreatePricingNoteAsync(networkId);
        ViewBag.EmployeePasswordMinLength = StrongPasswordRules.MinimumLength;
        ViewBag.WizardInitialStep = wizardStep;
    }

    private void ApplyAccountValidationToModelState(CreateEmployeeAccountValidationResult validation)
    {
        foreach (KeyValuePair<string, List<string>> kvp in validation.FieldErrors)
        {
            foreach (string message in kvp.Value)
            {
                ModelState.AddModelError(kvp.Key, message);
            }
        }

        foreach (string message in validation.GeneralErrors)
        {
            ModelState.AddModelError(string.Empty, message);
        }
    }

    private static object ToAccountValidationJson(CreateEmployeeAccountValidationResult validation)
    {
        return new
        {
            isValid = validation.IsValid,
            fieldErrors = validation.FieldErrors.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToArray()),
            generalErrors = validation.GeneralErrors.ToArray()
        };
    }

    private void MapIdentityErrorToModelState(IdentityError error)
    {
        string field = error.Code switch
        {
            "DuplicateUserName" or "InvalidUserName" => nameof(CreateEmployeeViewModel.UserName),
            "DuplicateEmail" or "InvalidEmail" => nameof(CreateEmployeeViewModel.Email),
            "PasswordTooShort" or "PasswordRequiresDigit" or "PasswordRequiresLower" or
            "PasswordRequiresUpper" or "PasswordRequiresNonAlphanumeric" or "PasswordRequiresUniqueChars"
                => nameof(CreateEmployeeViewModel.Password),
            _ => string.Empty
        };

        ModelState.AddModelError(field, error.Description);
    }

    private int ResolveWizardStepFromModelState()
    {
        static bool HasFieldError(Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateDictionary modelState, string key) =>
            modelState.TryGetValue(key, out Microsoft.AspNetCore.Mvc.ModelBinding.ModelStateEntry? entry) &&
            entry?.Errors.Count > 0;

        if (HasFieldError(ModelState, nameof(CreateEmployeeViewModel.Department)))
        {
            return 2;
        }

        string[] step1Keys =
        [
            nameof(CreateEmployeeViewModel.UserName),
            nameof(CreateEmployeeViewModel.Email),
            nameof(CreateEmployeeViewModel.Password),
            nameof(CreateEmployeeViewModel.ConfirmPassword),
            nameof(CreateEmployeeViewModel.PhoneNumber)
        ];

        if (step1Keys.Any(key => HasFieldError(ModelState, key)))
        {
            return 1;
        }

        return 4;
    }

    private async Task LoadEmployeePermissionMatrixAsync(int selectedNetworkId)
    {
        try
        {
            HashSet<string> enabledFeatureKeys = await GetEnabledFeatureKeysAsync(selectedNetworkId);
            List<Permission> allPermissions = await _context.Permissions.AsNoTracking().ToListAsync();
            List<EmployeeServicePermissionUiRow> rows = EmployeeServicePermissionMatrix.BuildRows(enabledFeatureKeys, allPermissions);
            ViewBag.EmployeeServicePermissionRows = rows;
            ViewBag.EmployeeDepartmentTemplates = EmployeeDepartmentTemplates.GetUiItems();
            ViewBag.EmployeeDepartmentGroups = EmployeeDepartmentTemplates.GetUiGroups();
            ViewBag.EmployeeDepartmentTemplatesJson = EmployeeDepartmentTemplates.BuildTemplatesJson(enabledFeatureKeys, allPermissions);
        }
        catch
        {
            ViewBag.EmployeeServicePermissionRows = new List<EmployeeServicePermissionUiRow>();
            ViewBag.EmployeeDepartmentTemplates = EmployeeDepartmentTemplates.GetUiItems();
            ViewBag.EmployeeDepartmentGroups = EmployeeDepartmentTemplates.GetUiGroups();
            ViewBag.EmployeeDepartmentTemplatesJson = "[]";
        }
    }

    private async Task LoadEmployeeCopySourcesAsync(int selectedNetworkId)
    {
        try
        {
            List<ApplicationUser> networkUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.NetworkId == selectedNetworkId)
                .OrderBy(u => u.FullName)
                .ThenBy(u => u.UserName)
                .ToListAsync();

            List<object> sources = new List<object>();
            foreach (ApplicationUser u in networkUsers)
            {
                if (u.Id == null)
                {
                    continue;
                }

                IList<string> roles = await _userManager.GetRolesAsync(u);
                if (!roles.Contains(RoleNames.CompanyEmployee) && !roles.Contains(RoleNames.EmployeeLegacy))
                {
                    continue;
                }

                List<int> permissionIds = await _context.UserPermissions
                    .AsNoTracking()
                    .Where(up => up.UserId == u.Id)
                    .Select(up => up.PermissionId)
                    .Distinct()
                    .ToListAsync();

                if (permissionIds.Count == 0 && u.EmployeeDepartment == EmployeeDepartment.None)
                {
                    continue;
                }

                int deptInt = (int)u.EmployeeDepartment;
                if (deptInt == (int)EmployeeDepartment.None && permissionIds.Count > 0)
                {
                    deptInt = (int)EmployeeDepartment.Custom;
                }

                string labelName = !string.IsNullOrWhiteSpace(u.FullName) ? u.FullName.Trim() : (u.UserName ?? u.Id);
                string deptName = EmployeeDepartmentTemplates.GetDisplayName((EmployeeDepartment)deptInt);
                sources.Add(new
                {
                    id = u.Id,
                    label = $"{labelName} — {deptName}",
                    department = deptInt,
                    permissionIds
                });
            }

            ViewBag.EmployeeCopySourcesJson = System.Text.Json.JsonSerializer.Serialize(sources);
        }
        catch
        {
            ViewBag.EmployeeCopySourcesJson = "[]";
        }
    }

    private bool ValidateEmployeeDepartmentSelection(
        EmployeeDepartment department,
        List<int>? selectedPermissionIds,
        string fieldName)
    {
        int permCount = (selectedPermissionIds ?? []).Distinct().Count();
        if (department == EmployeeDepartment.None && permCount == 0)
        {
            ModelState.AddModelError(fieldName, "اختر قسم الموظف أو حدّد الصلاحيات يدوياً (تخصيص).");
            return false;
        }

        return true;
    }

    private async Task<EmployeeDepartment> ResolveEmployeeDepartmentForSaveAsync(
        EmployeeDepartment department,
        List<int>? selectedPermissionIds,
        int selectedNetworkId)
    {
        List<int> selected = (selectedPermissionIds ?? []).Distinct().OrderBy(x => x).ToList();
        if (department == EmployeeDepartment.None)
        {
            return selected.Count > 0 ? EmployeeDepartment.Custom : EmployeeDepartment.None;
        }

        if (department == EmployeeDepartment.Custom)
        {
            return EmployeeDepartment.Custom;
        }

        HashSet<string> enabledFeatureKeys = await GetEnabledFeatureKeysAsync(selectedNetworkId);
        List<Permission> allPermissions = await _context.Permissions.AsNoTracking().ToListAsync();
        List<int> templateIds = EmployeeDepartmentTemplates
            .ResolvePermissionIds(department, enabledFeatureKeys, allPermissions)
            .OrderBy(x => x)
            .ToList();

        if (templateIds.Count > 0 && templateIds.SequenceEqual(selected))
        {
            return department;
        }

        return EmployeeDepartment.Custom;
    }

    private async Task LoadEmployeeCreatePricingNoteAsync(int selectedNetworkId)
    {
        try
        {
            Network? selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            UsageImportChargeEstimate estimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerUser,
                1);

            ViewBag.EmployeeCreateChargeHasPricing = estimate.HasCharge;
            ViewBag.EmployeeCreateChargeAmount = estimate.RequiredAmountSyp;
            ViewBag.EmployeeCreateChargeWalletBalance = estimate.WalletBalance;

            CreatePricingPreviewResult preview = await _pricingPreviewService.BuildAsync(
                selectedNetworkId,
                FeatureKeys.Users,
                PricingChargeUnit.PerUser,
                PricingPreviewCounterKeys.Employees);
            PricingPreviewViewBagMapper.Apply(ViewData, "EmployeeCreate", preview);

        }
        catch
        {
            ViewBag.EmployeeCreateChargeHasPricing = false;
            ViewBag.EmployeeCreateChargeAmount = 0m;
            ViewBag.EmployeeCreateChargeWalletBalance = 0m;
            PricingPreviewViewBagMapper.Apply(ViewData, "EmployeeCreate", PricingPreviewViewBagMapper.Empty());
        }
    }

    private async Task<HashSet<string>> GetEnabledFeatureKeysAsync(int selectedNetworkId)
    {
        HashSet<string> featureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            FeatureKeys.Users // صفحة إدارة المستخدمين نفسها
        };

        // إتاحة كل خدمات مصفوفة صلاحيات الموظف دائماً لمدير الشركة
        // حتى لو لم تُسجل اشتراكاتها بعد في NetworkServiceSubscriptions.
        foreach (string supportedKey in EmployeeServicePermissionMatrix.GetSupportedFeatureKeys())
        {
            featureKeys.Add(supportedKey);
        }

        int effectiveNetworkId = selectedNetworkId;
        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
        if (selectedNetwork?.ParentNetworkId != null)
        {
            effectiveNetworkId = selectedNetwork.ParentNetworkId.Value;
        }

        DateTime now = DateTime.Now;
        List<string> subscribedFeatureKeys = await _context.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s =>
                s.NetworkId == effectiveNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Active &&
                s.ExpiresAt > now)
            .Select(s => s.FeatureKey)
            .ToListAsync();

        foreach (string key in subscribedFeatureKeys)
        {
            featureKeys.Add(key);
        }

        // دعم التوافق مع الجدول القديم (NetworkFeatures) إن وُجدت بيانات فيه
        List<string> legacyEnabledFeatures = await _context.NetworkFeatures
            .AsNoTracking()
            .Where(f => f.NetworkId == effectiveNetworkId && f.IsEnabled)
            .Select(f => f.Key)
            .ToListAsync();

        foreach (string key in legacyEnabledFeatures)
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

        HashSet<string> enabledFeatureKeys = await GetEnabledFeatureKeysAsync(selectedNetworkId);
        List<Permission> allPermissions = await _context.Permissions.AsNoTracking().ToListAsync();
        HashSet<int> allowed = EmployeeServicePermissionMatrix.GetAllowedPermissionIds(enabledFeatureKeys, allPermissions);

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
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

        if (currentUser == null || !networkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        // إذا كان النوع هو "clients" نعرض صفحة خاصة بالمشتركين
        if (!string.IsNullOrWhiteSpace(type) &&
            type.Equals("clients", StringComparison.OrdinalIgnoreCase))
        {
            return await ClientsList(q, networkId.Value);
        }

        // جميع المستخدمين المرتبطين بهذه الشبكة (باستثناء مدير النظام)
        IQueryable<ApplicationUser> usersQuery = _userManager.Users
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

        List<ApplicationUser> users = await usersQuery.ToListAsync();

        List<global::RadaTik.Controllers.AdminUserListItem> model = new List<global::RadaTik.Controllers.AdminUserListItem>();
        foreach (ApplicationUser? u in users)
        {
            IList<string> roles = await _userManager.GetRolesAsync(u);

            model.Add(new global::RadaTik.Controllers.AdminUserListItem
            {
                Id = u.Id,
                UserName = u.UserName ?? "",
                FullName = u.FullName ?? "",
                Email = u.Email ?? "",
                PhoneNumber = u.PhoneNumber ?? "",
                IsActive = u.IsActive,
                Roles = string.Join(", ", roles),
                EmployeeDepartment = u.EmployeeDepartment,
                EmployeeDepartmentName = EmployeeDepartmentTemplates.GetDisplayName(u.EmployeeDepartment),
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
                    IEnumerable<string> roles = u.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(r => r.Trim());
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
        IQueryable<Client> clientsQuery = _context.Clients
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

        List<Client> clients = await clientsQuery.ToListAsync();

        List<global::RadaTik.Controllers.AdminClientListItem> model = clients.Select(c => new global::RadaTik.Controllers.AdminClientListItem
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

