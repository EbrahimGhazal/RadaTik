using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using System.Security.Cryptography;

namespace RadaTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class JoinRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISystemAdminPricingReadinessService _pricingReadiness;
    private readonly ILogger<JoinRequestsController> _logger;

    public JoinRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ISystemAdminPricingReadinessService pricingReadiness,
        ILogger<JoinRequestsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _pricingReadiness = pricingReadiness;
        _logger = logger;
    }

    /// <summary>
    /// SystemAdministrator-only: show NetworkAdministrator/CollectionPoint join requests.
    /// </summary>
    public async Task<IActionResult> Index(JoinRequestStatus? status = null, JoinRequestType? type = null)
    {
        IQueryable<JoinRequest> baseQuery = _context.JoinRequests
            .Include(j => j.ProcessedByUser)
            .Where(j => j.RequestType == JoinRequestType.NetworkAdministrator || j.RequestType == JoinRequestType.CollectionPoint);

        if (type.HasValue && (type == JoinRequestType.NetworkAdministrator || type == JoinRequestType.CollectionPoint))
        {
            baseQuery = baseQuery.Where(j => j.RequestType == type.Value);
        }

        IQueryable<JoinRequest> query = baseQuery;
        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        List<JoinRequest> requests = await query
            .OrderByDescending(j => j.CreatedDate)
            .ToListAsync();

        ViewBag.SelectedStatus = status;
        ViewBag.SelectedType = type;

        // Simple counts for quick filters
        ViewBag.CountPending = await baseQuery.CountAsync(j => j.Status == JoinRequestStatus.Pending);
        ViewBag.CountUnderReview = await baseQuery.CountAsync(j => j.Status == JoinRequestStatus.UnderReview);
        ViewBag.CountApproved = await baseQuery.CountAsync(j => j.Status == JoinRequestStatus.Approved);
        ViewBag.CountRejected = await baseQuery.CountAsync(j => j.Status == JoinRequestStatus.Rejected);

        return View(requests);
    }

    public async Task<IActionResult> Details(int id)
    {
        JoinRequest? request = await _context.JoinRequests
            .Include(j => j.ProcessedByUser)
            .FirstOrDefaultAsync(j => j.Id == id);

        if (request == null)
        {
            return NotFound();
        }

        if (request.RequestType != JoinRequestType.NetworkAdministrator &&
            request.RequestType != JoinRequestType.CollectionPoint)
        {
            return NotFound();
        }

        return View(request);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, JoinRequestStatus status, string? adminNotes)
    {
        JoinRequest? request = await _context.JoinRequests.FindAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        if (request.RequestType != JoinRequestType.NetworkAdministrator &&
            request.RequestType != JoinRequestType.CollectionPoint)
        {
            return NotFound();
        }

        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

        // عند القبول: إنشاء حساب المستخدم فعلياً ثم تحديث حالة الطلب
        if (status == JoinRequestStatus.Approved && request.Status != JoinRequestStatus.Approved)
        {
            SystemAdminPricingReadiness pricing = await _pricingReadiness.EvaluateAsync();
            if (!pricing.IsComplete)
            {
                TempData["Error"] =
                    "لا يمكن قبول طلبات الانضمام قبل إكمال تهيئة أسعار الخدمات من كتالوج الخدمات.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                string? generatedTempPassword;
                if (request.RequestType == JoinRequestType.NetworkAdministrator)
                {
                    generatedTempPassword = await ApproveNetworkAdminRequestAsync(request, currentUser, adminNotes);
                }
                else
                {
                    generatedTempPassword = await ApproveCollectionPointRequestAsync(request, currentUser, adminNotes);
                }
                if (!string.IsNullOrWhiteSpace(generatedTempPassword))
                {
                    TempData["GeneratedTempPassword"] = generatedTempPassword;
                    TempData["Info"] = "تم إنشاء كلمة مرور مؤقتة. شاركها مع مقدم الطلب عبر قناة آمنة.";
                }
                TempData["Success"] = request.RequestType == JoinRequestType.NetworkAdministrator
                    ? "تم قبول الطلب وإنشاء حساب مدير الشركة بنجاح."
                    : "تم قبول الطلب وإنشاء حساب نقطة التحصيل بنجاح.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في قبول طلب مدير شبكة #{RequestId}", request.Id);
                TempData["Error"] = ex.Message;
            }
            return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
        }

        request.Status = status;
        request.AdminNotes = adminNotes;
        request.UpdatedDate = DateTime.UtcNow;
        request.ProcessedDate = DateTime.UtcNow;
        request.ProcessedByUserId = currentUser?.Id;

        await _context.SaveChangesAsync();

        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
    }

    /// <summary>
    /// قبول طلب نقطة تحصيل وإنشاء الحساب مع الرصيد الابتدائي.
    /// </summary>
    private async Task<string?> ApproveCollectionPointRequestAsync(JoinRequest request, ApplicationUser? currentUser, string? adminNotesFromForm)
    {
        string requestedUserName = ExtractValue(request.Notes, "اسم المستخدم المطلوب:");
        if (string.IsNullOrWhiteSpace(requestedUserName))
        {
            requestedUserName = (request.Email ?? "").Split('@')[0];
        }

        string initialBalanceText = ExtractValue(request.Notes, "الرصيد الابتدائي المطلوب:");
        if (!decimal.TryParse(initialBalanceText, out decimal initialBalance))
        {
            initialBalance = 0m;
        }

        if (initialBalance < 0)
        {
            throw new Exception("لا يمكن قبول الطلب: الرصيد الابتدائي المطلوب لا يمكن أن يكون سالباً.");
        }

        string userName = await EnsureAvailableUserNameAsync(SanitizeUserName(requestedUserName));
        string? password = request.RequestedPassword?.Trim();
        bool generatedTemporaryPassword = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateTemporaryPassword();
            generatedTemporaryPassword = true;
        }

        ApplicationUser? existingByEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await _userManager.FindByEmailAsync(request.Email);
        ApplicationUser? existingByUserName = await _userManager.FindByNameAsync(userName);

        ApplicationUser user;
        if (existingByEmail != null)
        {
            user = existingByEmail;
            user.UserName = string.IsNullOrWhiteSpace(user.UserName) ? userName : user.UserName;
            user.FullName = string.IsNullOrWhiteSpace(user.FullName) ? request.FullName : user.FullName;
            user.PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? request.PhoneNumber : user.PhoneNumber;
            user.Address = string.IsNullOrWhiteSpace(user.Address) ? request.Address : user.Address;
            user.IsActive = true;
            user.NetworkId = null;
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            IdentityResult resetResult = await JoinRequestPasswordHelper.ResetPasswordAsync(_userManager, user, password);
            if (!resetResult.Succeeded)
            {
                throw new Exception($"فشل في تحديث كلمة مرور الحساب الحالي: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }

            JoinRequestPasswordHelper.ApplyPostProvisionPasswordPolicy(user, password, generatedTemporaryPassword);
            await _userManager.UpdateAsync(user);
        }
        else
        {
            if (existingByUserName != null)
            {
                userName = await EnsureAvailableUserNameAsync($"{userName}_{RandomNumberGenerator.GetInt32(100, 999)}");
            }

            user = new ApplicationUser
            {
                UserName = userName,
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber ?? "",
                Address = request.Address,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                EmailConfirmed = true,
                NetworkId = null
            };

            IdentityResult createResult = await JoinRequestPasswordHelper.CreateUserAsync(_userManager, user, password);
            if (!createResult.Succeeded)
            {
                throw new Exception($"فشل في إنشاء الحساب: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            JoinRequestPasswordHelper.ApplyPostProvisionPasswordPolicy(user, password, generatedTemporaryPassword);
            await _userManager.UpdateAsync(user);
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.CollectionPoint))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.CollectionPoint);
        }

        CollectionPointAccount? account = await _context.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.NetworkId == null);

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = user.Id,
                NetworkId = null,
                Balance = initialBalance,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };
            _context.CollectionPointAccounts.Add(account);
        }
        else
        {
            account.Balance += initialBalance;
            account.UpdatedAt = DateTime.Now;
            _context.Update(account);
        }

        await _context.SaveChangesAsync();

        request.Status = JoinRequestStatus.Approved;
        request.UpdatedDate = DateTime.UtcNow;
        request.ProcessedByUserId = currentUser?.Id;
        request.ProcessedDate = DateTime.UtcNow;
        request.AdminNotes = $"تمت معالجة طلب نقطة التحصيل بنجاح. اسم المستخدم: {user.UserName}";
        request.RequestedPassword = password;

        await _context.SaveChangesAsync();
        _logger.LogInformation("تم قبول طلب نقطة تحصيل وإنشاء الحساب: {UserName} ({Email})", user.UserName, request.Email);

        return generatedTemporaryPassword ? password : null;
    }

    /// <summary>
    /// قبول طلب مدير شبكة وإنشاء حسابه في Identity
    /// </summary>
    private async Task<string?> ApproveNetworkAdminRequestAsync(JoinRequest request, ApplicationUser? currentUser, string? adminNotesFromForm)
    {
        string requestedUserName = ExtractValue(request.Notes, "اسم المستخدم المطلوب:", "اسم المستخدم:");
        if (string.IsNullOrWhiteSpace(requestedUserName))
        {
            requestedUserName = ExtractValue(adminNotesFromForm, "اسم المستخدم المطلوب:", "اسم المستخدم:");
        }
        if (string.IsNullOrWhiteSpace(requestedUserName))
        {
            requestedUserName = (request.Email ?? "").Split('@')[0];
        }
        string userName = await EnsureAvailableUserNameAsync(SanitizeUserName(requestedUserName));

        string? password = request.RequestedPassword?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            // Backward compatibility: old requests may still carry password text in notes.
            password = ExtractValue(adminNotesFromForm, "كلمة المرور المطلوبة:", "كلمة المرور:");
        }
        bool generatedTemporaryPassword = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateTemporaryPassword();
            generatedTemporaryPassword = true;
        }

        ApplicationUser? existingByEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await _userManager.FindByEmailAsync(request.Email);
        ApplicationUser? existingByUserName = await _userManager.FindByNameAsync(userName);

        ApplicationUser user;

        if (existingByEmail != null)
        {
            // Reuse existing account for same email instead of failing approval.
            user = existingByEmail;
            user.FullName = string.IsNullOrWhiteSpace(user.FullName) ? request.FullName : user.FullName;
            user.PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? request.PhoneNumber : user.PhoneNumber;
            user.IsActive = true;
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            IdentityResult resetResult = await JoinRequestPasswordHelper.ResetPasswordAsync(_userManager, user, password);
            if (!resetResult.Succeeded)
            {
                throw new Exception($"فشل في تحديث كلمة مرور الحساب الحالي: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }

            JoinRequestPasswordHelper.ApplyPostProvisionPasswordPolicy(user, password, generatedTemporaryPassword);
            await _userManager.UpdateAsync(user);
        }
        else
        {
            if (existingByUserName != null)
            {
                userName = await EnsureAvailableUserNameAsync($"{userName}_{RandomNumberGenerator.GetInt32(100, 999)}");
            }

            user = new ApplicationUser
            {
                UserName = userName,
                Email = request.Email,
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber ?? "",
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                EmailConfirmed = true
            };

            IdentityResult createResult = await JoinRequestPasswordHelper.CreateUserAsync(_userManager, user, password);
            if (!createResult.Succeeded)
            {
                throw new Exception($"فشل في إنشاء الحساب: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }

            JoinRequestPasswordHelper.ApplyPostProvisionPasswordPolicy(user, password, generatedTemporaryPassword);
            await _userManager.UpdateAsync(user);
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.NetworkAdministrator))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.NetworkAdministrator);
        }

        request.Status = JoinRequestStatus.Approved;
        request.UpdatedDate = DateTime.UtcNow;
        request.ProcessedByUserId = currentUser?.Id;
        request.ProcessedDate = DateTime.UtcNow;
        request.AdminNotes = $"تمت معالجة الطلب بنجاح. اسم المستخدم: {user.UserName}";
        request.RequestedPassword = password;

        await _context.SaveChangesAsync();
        _logger.LogInformation("تم قبول طلب مدير شبكة وإنشاء الحساب: {UserName} ({Email})", userName, request.Email);
        return generatedTemporaryPassword ? password : null;
    }

    private static string ExtractValue(string? source, params string[] labels)
    {
        if (string.IsNullOrWhiteSpace(source) || labels.Length == 0)
        {
            return string.Empty;
        }

        foreach (string label in labels)
        {
            int idx = source.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                continue;
            }

            string value = source[(idx + label.Length)..].Trim();
            int lineBreak = value.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0)
            {
                value = value[..lineBreak].Trim();
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }
        return string.Empty;
    }

    private static string SanitizeUserName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return "network_admin";
        }

        string filtered = new string(raw.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-').ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "network_admin" : filtered;
    }

    private async Task<string> EnsureAvailableUserNameAsync(string baseUserName)
    {
        string candidate = baseUserName;
        ApplicationUser? exists = await _userManager.FindByNameAsync(candidate);
        if (exists == null)
        {
            return candidate;
        }

        for (int i = 0; i < 20; i++)
        {
            candidate = $"{baseUserName}{RandomNumberGenerator.GetInt32(100, 999)}";
            exists = await _userManager.FindByNameAsync(candidate);
            if (exists == null)
            {
                return candidate;
            }
        }

        return $"{baseUserName}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static string GenerateTemporaryPassword()
    {
        char upper = (char)('A' + RandomNumberGenerator.GetInt32(0, 26));
        char lower = (char)('a' + RandomNumberGenerator.GetInt32(0, 26));
        char digit = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        char[] symbols = new[] { '@', '#', '!', '$', '%' };
        char symbol = symbols[RandomNumberGenerator.GetInt32(0, symbols.Length)];
        int suffix = RandomNumberGenerator.GetInt32(10000, 99999);
        return $"Rt{upper}{lower}{digit}{symbol}{suffix}";
    }
}

