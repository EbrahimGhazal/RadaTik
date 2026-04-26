using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using System.Linq;
using System.Threading.Tasks;

namespace RadTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// Controller لإدارة طلبات استعادة كلمة المرور ضمن منطقة مدير الشركة (networkManager)
/// ليبقى التصميم والـ sidebar موحدين مع باقي صفحات مدير الشركة.
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.PasswordResets)]
public class JoinRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<JoinRequestsController> _logger;

    public JoinRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<JoinRequestsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// إعادة توجيه Index إلى PasswordResets (للتوافق مع الروابط التي تستخدم /Index)
    /// </summary>
    public IActionResult Index() => RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });

    /// <summary>
    /// توافق خلفي: بعض الروابط القديمة قد تشير إلى /networkManager/JoinRequests/Details/{id}
    /// بينما صفحة مدير الشركة هنا مخصصة لطلبات استعادة كلمة المرور وتفاصيلها تُدار ضمن القائمة/Modal.
    /// لذلك نعيد التوجيه إلى صفحة الطلبات بدل 404.
    /// </summary>
    [HttpGet]
    public IActionResult Details(int id) =>
        RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });

    /// <summary>
    /// توافق خلفي/حماية: بعض الروابط أو الفورمات القديمة قد تشير إلى /networkManager/JoinRequests/UpdateStatus
    /// بينما "تحديث حالة طلب الانضمام" خاص بمدير النظام ضمن /systemAdmin/JoinRequests.
    /// </summary>
    [HttpGet]
    public IActionResult UpdateStatus()
    {
        TempData["Error"] = "هذه العملية خاصة بمدير النظام. تم إعادتك إلى صفحة طلبات استعادة كلمة المرور.";
        return RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });
    }

    /// <summary>
    /// تم تعطيل صفحة طلبات استعادة كلمة المرور ضمن واجهة مدير الشركة.
    /// أي وصول مباشر لهذا المسار يُعاد توجيهه إلى الملف الشخصي.
    /// </summary>
    public IActionResult PasswordResets(PasswordResetStatus? status = null)
    {
        TempData["Info"] = "تم إلغاء تبويب طلبات استعادة كلمة المرور من واجهة مدير الشركة.";
        return RedirectToRoute("networkManager-account-profile");
    }

    /// <summary>
    /// عرض تفاصيل طلب استعادة كلمة مرور (إعادة التوجيه إلى قائمة الطلبات - الصفحة تستخدم Modal)
    /// </summary>
    public async Task<IActionResult> PasswordResetDetails(int id)
    {
        var request = await _context.PasswordResetRequests.FindAsync(id);
        if (request == null)
        {
            return NotFound();
        }
        return RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });
    }

    /// <summary>
    /// إعادة تعيين كلمة مرور المستخدم
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(int requestId, string newPassword)
    {
        var request = await _context.PasswordResetRequests
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == requestId);

        if (request == null || request.User == null)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
        {
            TempData["Error"] = "كلمة المرور يجب أن تكون 6 أحرف على الأقل";
            return RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });
        }

        var currentUser = await _userManager.GetUserAsync(User);

        var token = await _userManager.GeneratePasswordResetTokenAsync(request.User);
        var result = await _userManager.ResetPasswordAsync(request.User, token, newPassword);

        if (result.Succeeded)
        {
            request.Status = PasswordResetStatus.Completed;
            request.ProcessedDate = DateTime.Now;
            request.ProcessedByUserId = currentUser?.Id;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"تم إعادة تعيين كلمة مرور المستخدم {request.User.Email} بواسطة {currentUser?.UserName}");

            TempData["Success"] = $"تم إعادة تعيين كلمة المرور للمستخدم {request.User.FullName ?? request.User.UserName} بنجاح";
        }
        else
        {
            TempData["Error"] = "فشل في إعادة تعيين كلمة المرور: " + string.Join(", ", result.Errors.Select(e => e.Description));
        }

        return RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });
    }

    /// <summary>
    /// رفض طلب استعادة كلمة المرور
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectPasswordReset(int requestId, string? notes)
    {
        var request = await _context.PasswordResetRequests.FindAsync(requestId);
        if (request == null)
        {
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);

        request.Status = PasswordResetStatus.Cancelled;
        request.Notes = notes;
        request.ProcessedDate = DateTime.Now;
        request.ProcessedByUserId = currentUser?.Id;
        await _context.SaveChangesAsync();

        _logger.LogInformation($"تم رفض طلب استعادة كلمة المرور #{requestId}");

        TempData["Success"] = "تم رفض الطلب";
        return RedirectToRoute("networkManager-joinRequests-passwordResets", new { action = nameof(PasswordResets) });
    }
}
