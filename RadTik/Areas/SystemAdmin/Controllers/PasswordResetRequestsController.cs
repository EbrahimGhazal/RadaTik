using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class PasswordResetRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<PasswordResetRequestsController> _logger;

    public PasswordResetRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<PasswordResetRequestsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(PasswordResetStatus? status = null)
    {
        ViewData["Title"] = "طلبات إعادة تعيين كلمة المرور";

        var query = _context.PasswordResetRequests
            .Include(p => p.User)
            .Include(p => p.ProcessedByUser)
            .Where(p => p.ResetMethod == PasswordResetMethod.AdminRequest);

        if (status.HasValue)
        {
            query = query.Where(p => p.Status == status.Value);
        }

        var items = await query
            .OrderByDescending(p => p.CreatedDate)
            .Take(300)
            .ToListAsync();

        ViewBag.SelectedStatus = status;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(int requestId, string newPassword)
    {
        var request = await _context.PasswordResetRequests
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == requestId && p.ResetMethod == PasswordResetMethod.AdminRequest);

        if (request == null || request.User == null)
        {
            if (IsAjaxRequest()) return Json(new { ok = false, message = "الطلب غير موجود." });
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
        {
            if (IsAjaxRequest()) return Json(new { ok = false, message = "كلمة المرور يجب أن تكون 6 أحرف على الأقل." });
            TempData["Error"] = "كلمة المرور يجب أن تكون 6 أحرف على الأقل.";
            return RedirectToRoute("systemAdmin-passwordResetRequests");
        }

        var currentUser = await _userManager.GetUserAsync(User);
        var token = await _userManager.GeneratePasswordResetTokenAsync(request.User);
        var result = await _userManager.ResetPasswordAsync(request.User, token, newPassword);

        if (!result.Succeeded)
        {
            if (IsAjaxRequest()) return Json(new { ok = false, message = "فشل تغيير كلمة المرور: " + string.Join(", ", result.Errors.Select(e => e.Description)) });
            TempData["Error"] = "فشل تغيير كلمة المرور: " + string.Join(", ", result.Errors.Select(e => e.Description));
            return RedirectToRoute("systemAdmin-passwordResetRequests");
        }

        request.Status = PasswordResetStatus.Completed;
        request.ProcessedDate = DateTime.Now;
        request.ProcessedByUserId = currentUser?.Id;
        request.Notes = $"تم تغيير كلمة المرور بواسطة مدير النظام بتاريخ {DateTime.Now:yyyy/MM/dd HH:mm}.";
        await _context.SaveChangesAsync();

        _logger.LogInformation("SystemAdmin reset password for user {UserId} from request {RequestId}", request.UserId, request.Id);
        if (IsAjaxRequest()) return Json(new { ok = true, message = "تم تغيير كلمة المرور بنجاح." });
        TempData["Success"] = "تم تغيير كلمة المرور بنجاح. يرجى إبلاغ مدير الشركة بكلمة المرور الجديدة.";
        return RedirectToRoute("systemAdmin-passwordResetRequests");
    }

    [HttpGet]
    public IActionResult ResetUserPassword()
    {
        TempData["Error"] = "العملية غير صالحة عبر الرابط المباشر. استخدم زر الحفظ من صفحة الطلبات.";
        return RedirectToRoute("systemAdmin-passwordResetRequests");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int requestId, string? notes)
    {
        var request = await _context.PasswordResetRequests
            .FirstOrDefaultAsync(p => p.Id == requestId && p.ResetMethod == PasswordResetMethod.AdminRequest);

        if (request == null)
        {
            if (IsAjaxRequest()) return Json(new { ok = false, message = "الطلب غير موجود." });
            return NotFound();
        }

        var currentUser = await _userManager.GetUserAsync(User);
        request.Status = PasswordResetStatus.Cancelled;
        request.ProcessedDate = DateTime.Now;
        request.ProcessedByUserId = currentUser?.Id;
        request.Notes = string.IsNullOrWhiteSpace(notes) ? "تم رفض الطلب من مدير النظام." : notes.Trim();
        await _context.SaveChangesAsync();

        if (IsAjaxRequest()) return Json(new { ok = true, message = "تم رفض الطلب." });
        TempData["Success"] = "تم رفض طلب إعادة التعيين.";
        return RedirectToRoute("systemAdmin-passwordResetRequests");
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }
}

