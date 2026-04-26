using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using System.Security.Cryptography;

namespace RadTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class JoinRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<JoinRequestsController> _logger;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService;

    public JoinRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<JoinRequestsController> logger,
        IUsageBasedSubscriptionChargeService usageChargeService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _usageChargeService = usageChargeService;
    }

    /// <summary>
    /// SystemAdministrator-only: show NetworkAdministrator/CollectionPoint join requests.
    /// </summary>
    public async Task<IActionResult> Index(JoinRequestStatus? status = null, JoinRequestType? type = null)
    {
        var baseQuery = _context.JoinRequests
            .Include(j => j.ProcessedByUser)
            .Where(j => j.RequestType == JoinRequestType.NetworkAdministrator || j.RequestType == JoinRequestType.CollectionPoint);

        if (type.HasValue && (type == JoinRequestType.NetworkAdministrator || type == JoinRequestType.CollectionPoint))
        {
            baseQuery = baseQuery.Where(j => j.RequestType == type.Value);
        }

        var query = baseQuery;
        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        var requests = await query
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
        var request = await _context.JoinRequests
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
        var request = await _context.JoinRequests.FindAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        if (request.RequestType != JoinRequestType.NetworkAdministrator &&
            request.RequestType != JoinRequestType.CollectionPoint)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);

        // عند القبول: إنشاء حساب المستخدم فعلياً ثم تحديث حالة الطلب
        if (status == JoinRequestStatus.Approved && request.Status != JoinRequestStatus.Approved)
        {
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

        TempData["Success"] = "تم تحديث حالة الطلب بنجاح.";
        return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
    }

    /// <summary>
    /// قبول طلب نقطة تحصيل وإنشاء الحساب مع الرصيد الابتدائي.
    /// </summary>
    private async Task<string?> ApproveCollectionPointRequestAsync(JoinRequest request, ApplicationUser? currentUser, string? adminNotesFromForm)
    {
        var requestedUserName = ExtractValue(request.Notes, "اسم المستخدم المطلوب:");
        if (string.IsNullOrWhiteSpace(requestedUserName))
        {
            requestedUserName = (request.Email ?? "").Split('@')[0];
        }

        var requestedNetworkIdText = ExtractValue(request.Notes, "معرّف الشبكة:");
        if (!int.TryParse(requestedNetworkIdText, out var requestedNetworkId) || requestedNetworkId <= 0)
        {
            throw new Exception("لا يمكن قبول الطلب: معرّف الشبكة غير صالح أو غير موجود في بيانات الطلب.");
        }

        var initialBalanceText = ExtractValue(request.Notes, "الرصيد الابتدائي المطلوب:");
        if (!decimal.TryParse(initialBalanceText, out var initialBalance))
        {
            initialBalance = 0m;
        }

        if (initialBalance < 0)
        {
            throw new Exception("لا يمكن قبول الطلب: الرصيد الابتدائي المطلوب لا يمكن أن يكون سالباً.");
        }

        var requestedNetwork = await _context.Networks.FirstOrDefaultAsync(n => n.Id == requestedNetworkId);
        if (requestedNetwork == null)
        {
            throw new Exception("لا يمكن قبول الطلب: الشبكة المحددة غير موجودة.");
        }

        var companyNetworkId = requestedNetwork.ParentNetworkId ?? requestedNetwork.Id;
        var companyNetwork = requestedNetwork.ParentNetworkId.HasValue
            ? await _context.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId)
            : requestedNetwork;

        if (companyNetwork == null)
        {
            throw new Exception("لا يمكن قبول الطلب: شبكة الشركة الأساسية غير موجودة.");
        }

        if (initialBalance > 0 && companyNetwork.Balance < initialBalance)
        {
            throw new Exception($"لا يمكن قبول الطلب: رصيد محفظة الشركة غير كافٍ. الرصيد الحالي: {companyNetwork.Balance:N2} ل.س والمطلوب: {initialBalance:N2} ل.س.");
        }

        var userName = await EnsureAvailableUserNameAsync(SanitizeUserName(requestedUserName));
        var password = request.RequestedPassword?.Trim();
        var generatedTemporaryPassword = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateTemporaryPassword();
            generatedTemporaryPassword = true;
        }

        var existingByEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await _userManager.FindByEmailAsync(request.Email);
        var existingByUserName = await _userManager.FindByNameAsync(userName);

        ApplicationUser user;
        if (existingByEmail != null)
        {
            user = existingByEmail;
            user.UserName = string.IsNullOrWhiteSpace(user.UserName) ? userName : user.UserName;
            user.FullName = string.IsNullOrWhiteSpace(user.FullName) ? request.FullName : user.FullName;
            user.PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? request.PhoneNumber : user.PhoneNumber;
            user.Address = string.IsNullOrWhiteSpace(user.Address) ? request.Address : user.Address;
            user.IsActive = true;
            user.NetworkId = requestedNetworkId;
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, password);
            if (!resetResult.Succeeded)
            {
                throw new Exception($"فشل في تحديث كلمة مرور الحساب الحالي: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }
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
                NetworkId = requestedNetworkId
            };

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new Exception($"فشل في إنشاء الحساب: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
        }

        if (!await _userManager.IsInRoleAsync(user, RoleNames.CollectionPoint))
        {
            await _userManager.AddToRoleAsync(user, RoleNames.CollectionPoint);
        }

        var account = await _context.CollectionPointAccounts
            .FirstOrDefaultAsync(a => a.UserId == user.Id && a.NetworkId == requestedNetworkId);

        if (account == null)
        {
            account = new CollectionPointAccount
            {
                UserId = user.Id,
                NetworkId = requestedNetworkId,
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

        if (initialBalance > 0)
        {
            var previousCompanyBalance = companyNetwork.Balance;
            companyNetwork.Balance -= initialBalance;

            _context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
            {
                NetworkId = companyNetwork.Id,
                Type = NetworkWalletTransactionType.Adjustment,
                SignedAmount = -initialBalance,
                PreviousBalance = previousCompanyBalance,
                NewBalance = companyNetwork.Balance,
                CreatedByUserId = currentUser?.Id ?? string.Empty,
                CreatedAt = DateTime.Now,
                Notes = $"حسم رصيد ابتدائي لنقطة التحصيل الجديدة: {request.FullName} ({user.UserName})"
            });
        }

        await _context.SaveChangesAsync();

        if (currentUser?.Id != null)
        {
            await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, currentUser.Id, PricingChargeUnit.PerCollectionPoint);
            await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, currentUser.Id, PricingChargeUnit.PerUser);
        }

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
        var requestedUserName = ExtractValue(request.Notes, "اسم المستخدم المطلوب:", "اسم المستخدم:");
        if (string.IsNullOrWhiteSpace(requestedUserName))
        {
            requestedUserName = ExtractValue(adminNotesFromForm, "اسم المستخدم المطلوب:", "اسم المستخدم:");
        }
        if (string.IsNullOrWhiteSpace(requestedUserName))
        {
            requestedUserName = (request.Email ?? "").Split('@')[0];
        }
        var userName = await EnsureAvailableUserNameAsync(SanitizeUserName(requestedUserName));

        var password = request.RequestedPassword?.Trim();
        if (string.IsNullOrWhiteSpace(password))
        {
            // Backward compatibility: old requests may still carry password text in notes.
            password = ExtractValue(adminNotesFromForm, "كلمة المرور المطلوبة:", "كلمة المرور:");
        }
        var generatedTemporaryPassword = false;
        if (string.IsNullOrWhiteSpace(password))
        {
            password = GenerateTemporaryPassword();
            generatedTemporaryPassword = true;
        }

        var existingByEmail = string.IsNullOrWhiteSpace(request.Email)
            ? null
            : await _userManager.FindByEmailAsync(request.Email);
        var existingByUserName = await _userManager.FindByNameAsync(userName);

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

            // Ensure provided/generated password becomes effective even for reused accounts.
            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, password);
            if (!resetResult.Succeeded)
            {
                throw new Exception($"فشل في تحديث كلمة مرور الحساب الحالي: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
            }
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

            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                throw new Exception($"فشل في إنشاء الحساب: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
            }
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
        if (string.IsNullOrWhiteSpace(source) || labels.Length == 0) return string.Empty;
        foreach (var label in labels)
        {
            var idx = source.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var value = source[(idx + label.Length)..].Trim();
            var lineBreak = value.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0) value = value[..lineBreak].Trim();
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return string.Empty;
    }

    private static string SanitizeUserName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "network_admin";
        var filtered = new string(raw.Trim().Where(ch => char.IsLetterOrDigit(ch) || ch == '.' || ch == '_' || ch == '-').ToArray());
        return string.IsNullOrWhiteSpace(filtered) ? "network_admin" : filtered;
    }

    private async Task<string> EnsureAvailableUserNameAsync(string baseUserName)
    {
        var candidate = baseUserName;
        var exists = await _userManager.FindByNameAsync(candidate);
        if (exists == null) return candidate;

        for (var i = 0; i < 20; i++)
        {
            candidate = $"{baseUserName}{RandomNumberGenerator.GetInt32(100, 999)}";
            exists = await _userManager.FindByNameAsync(candidate);
            if (exists == null) return candidate;
        }

        return $"{baseUserName}{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }

    private static string GenerateTemporaryPassword()
    {
        var upper = (char)('A' + RandomNumberGenerator.GetInt32(0, 26));
        var lower = (char)('a' + RandomNumberGenerator.GetInt32(0, 26));
        var digit = (char)('0' + RandomNumberGenerator.GetInt32(0, 10));
        var symbols = new[] { '@', '#', '!', '$', '%' };
        var symbol = symbols[RandomNumberGenerator.GetInt32(0, symbols.Length)];
        var suffix = RandomNumberGenerator.GetInt32(10000, 99999);
        return $"Rt{upper}{lower}{digit}{symbol}{suffix}";
    }
}

