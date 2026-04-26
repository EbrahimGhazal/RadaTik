using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Middleware;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.Spa;

namespace RadTik.Controllers;

/// <summary>
/// مصادقة SPA تحت /app: التحقق من المستخدم وكلمة المرور عبر نفس قاعدة البيانات و Identity المستخدمة في MVC.
/// </summary>
[ApiController]
[Route("api/spa-auth")]
[IgnoreAntiforgeryToken]
public class SpaAuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SpaAuthController> _logger;

    public SpaAuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ApplicationDbContext context,
        ILogger<SpaAuthController> logger)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _context = context;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] SpaLoginRequest? body)
    {
        if (body == null || string.IsNullOrWhiteSpace(body.UserName) || string.IsNullOrWhiteSpace(body.Password))
        {
            return BadRequest(new { ok = false, message = "يرجى إدخال اسم المستخدم وكلمة المرور." });
        }

        var userNameInput = body.UserName.Trim();
        var password = body.Password;

        var user = await _userManager.FindByNameAsync(userNameInput);
        if (user == null)
        {
            user = await _userManager.FindByEmailAsync(userNameInput);
        }

        if (user == null)
        {
            var pendingOrRejectedRequest = await _context.JoinRequests
                .Where(r => r.RequestType == JoinRequestType.NetworkAdministrator)
                .OrderByDescending(r => r.CreatedDate)
                .FirstOrDefaultAsync(r =>
                    r.Email == userNameInput ||
                    (r.Notes != null && r.Notes.Contains(userNameInput)));

            if (pendingOrRejectedRequest != null)
            {
                if (pendingOrRejectedRequest.Status == JoinRequestStatus.Pending ||
                    pendingOrRejectedRequest.Status == JoinRequestStatus.UnderReview)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = "طلب إنشاء حساب مدير الشركة قيد الانتظار. يرجى الانتظار حتى تتم الموافقة.",
                    });
                }

                if (pendingOrRejectedRequest.Status == JoinRequestStatus.Rejected)
                {
                    var reason = string.IsNullOrWhiteSpace(pendingOrRejectedRequest.AdminNotes)
                        ? "لم يتم تحديد السبب."
                        : pendingOrRejectedRequest.AdminNotes;
                    return BadRequest(new
                    {
                        ok = false,
                        message = $"تم رفض طلب إنشاء حساب مدير الشركة. السبب: {reason}",
                    });
                }
            }

            return Unauthorized(new { ok = false, message = "اسم المستخدم أو كلمة المرور غير صحيحة." });
        }

        if (!user.IsActive && !user.ClientId.HasValue)
        {
            return BadRequest(new
            {
                ok = false,
                message = "تم تجميد حسابك من قبل إدارة النظام. يرجى التواصل مع الدعم.",
            });
        }

        var signInResult = await _signInManager.PasswordSignInAsync(
            user.UserName!,
            password,
            isPersistent: false,
            lockoutOnFailure: true);

        if (!signInResult.Succeeded)
        {
            return Unauthorized(new { ok = false, message = "اسم المستخدم أو كلمة المرور غير صحيحة." });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var spaRole = MapToSpaRole(roles);
        if (spaRole == null)
        {
            await _signInManager.SignOutAsync();
            return BadRequest(new
            {
                ok = false,
                message = "لا يوجد دور مرتبط بهذا الحساب مدعوم في واجهة /app حالياً.",
            });
        }

        HttpContext.Session.Remove(AreaIsolationMiddleware.SessionKeyActiveArea);
        var activeArea = ResolveActiveArea(roles);
        if (!string.IsNullOrWhiteSpace(activeArea))
        {
            HttpContext.Session.SetString(AreaIsolationMiddleware.SessionKeyActiveArea, activeArea);
        }

        _logger.LogInformation("SPA login succeeded for {UserName}", user.UserName);

        var displayEmail = user.Email ?? user.UserName ?? "";
        var fullName = string.IsNullOrWhiteSpace(user.FullName) ? (user.UserName ?? displayEmail) : user.FullName!;

        return Ok(new
        {
            ok = true,
            user = new
            {
                id = user.Id,
                email = displayEmail,
                fullName,
                role = spaRole,
            },
        });
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        HttpContext.Session.Remove(AreaIsolationMiddleware.SessionKeyActiveArea);
        return Ok(new { ok = true });
    }

    [HttpGet("me")]
    [AllowAnonymous]
    public async Task<IActionResult> Me()
    {
        if (User?.Identity?.IsAuthenticated != true)
        {
            return Unauthorized(new { ok = false, message = "unauthenticated" });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(new { ok = false, message = "unauthenticated" });
        }

        var roles = await _userManager.GetRolesAsync(user);
        var spaRole = MapToSpaRole(roles);
        if (spaRole == null)
        {
            return Forbid();
        }

        var displayEmail = user.Email ?? user.UserName ?? "";
        var fullName = string.IsNullOrWhiteSpace(user.FullName) ? (user.UserName ?? displayEmail) : user.FullName!;
        return Ok(new
        {
            ok = true,
            user = new
            {
                id = user.Id,
                email = displayEmail,
                fullName,
                role = spaRole,
            },
        });
    }

    private static string? MapToSpaRole(IList<string> roles)
    {
        if (roles.Contains(RoleNames.SystemAdministrator))
            return "system_admin";
        if (roles.Contains(RoleNames.CollectionPoint))
            return "collection_point";
        if (roles.Contains(RoleNames.Client))
            return "client";
        if (roles.Contains(RoleNames.CompanyEmployee) || roles.Contains(RoleNames.EmployeeLegacy))
            return "employee";
        if (roles.Contains(RoleNames.SystemEmployee))
            return "employee";
        if (roles.Contains(RoleNames.NetworkAdministrator))
            return "company_manager";
        return null;
    }

    /// <summary>نفس ترتيب تعيين المنطقة النشطة عند تسجيل الدخول في AccountController.</summary>
    private static string? ResolveActiveArea(IList<string> roles)
    {
        if (roles.Contains(RoleNames.SystemAdministrator))
            return "SystemAdmin";
        if (roles.Contains(RoleNames.CollectionPoint))
            return "CollectionPoint";
        if (roles.Contains(RoleNames.Client))
            return "ClientPortal";
        if (roles.Contains(RoleNames.CompanyEmployee) || roles.Contains(RoleNames.EmployeeLegacy))
            return "CompanyEmployee";
        if (roles.Contains(RoleNames.NetworkAdministrator))
            return "CompanyAdmin";
        return null;
    }
}
