using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace RadaTik.Controllers
{
    /// <summary>
    /// Controller لإدارة طلبات الانضمام وطلبات استعادة كلمة المرور
    /// </summary>
    // سياسة المشروع حسب المتطلبات:
    // - طلبات مدراء الشركات (JoinRequestType.NetworkAdministrator): يعالجها مدير النظام فقط
    // - طلبات العملاء/الموظفين: يعالجها مدير الشركة (ويستطيع مدير النظام أيضاً الإطلاع)
    [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
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
        /// عرض جميع طلبات الانضمام
        /// </summary>
        public async Task<IActionResult> Index(JoinRequestType? type = null, JoinRequestStatus? status = null)
        {
            // قيد صلاحيات: مدير الشركة لا يمكنه رؤية طلبات مدراء الشركات
            if (type == JoinRequestType.NetworkAdministrator && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                return Forbid();
            }

            IQueryable<JoinRequest> query = _context.JoinRequests
                .Include(j => j.RequestedProfile)
                .Include(j => j.ProcessedByUser)
                .AsQueryable();

            if (type.HasValue)
            {
                query = query.Where(j => j.RequestType == type.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(j => j.Status == status.Value);
            }

            List<JoinRequest> requests = await query
                .OrderByDescending(j => j.CreatedDate)
                .ToListAsync();

            ViewBag.SelectedType = type;
            ViewBag.SelectedStatus = status;

            // إحصائيات
            ViewBag.PendingCount = await _context.JoinRequests.CountAsync(j => j.Status == JoinRequestStatus.Pending);
            ViewBag.ClientRequestsCount = await _context.JoinRequests.CountAsync(j => j.RequestType == JoinRequestType.Client);
            ViewBag.EmployeeRequestsCount = await _context.JoinRequests.CountAsync(j => j.RequestType == JoinRequestType.Employee);
            ViewBag.NetworkAdminRequestsCount = await _context.JoinRequests.CountAsync(j => j.RequestType == JoinRequestType.NetworkAdministrator);

            return View(requests);
        }

        /// <summary>
        /// عرض تفاصيل طلب انضمام
        /// </summary>
        public async Task<IActionResult> Details(int id)
        {
            JoinRequest? request = await _context.JoinRequests
                .Include(j => j.RequestedProfile)
                .Include(j => j.ProcessedByUser)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            // قيد صلاحيات: طلبات مدراء الشركات يراها مدير النظام فقط
            if (request.RequestType == JoinRequestType.NetworkAdministrator && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                return Forbid();
            }

            return View(request);
        }

        /// <summary>
        /// تحديث حالة الطلب
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int id, JoinRequestStatus status, string? adminNotes)
        {
            JoinRequest? request = await _context.JoinRequests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            // قيد صلاحيات: طلبات مدراء الشركات يوافق/يرفض عليها مدير النظام فقط
            if (request.RequestType == JoinRequestType.NetworkAdministrator && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                return Forbid();
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            // إذا كان الطلب لمدير شبكة وتم قبوله، إنشاء الحساب
            if (request.RequestType == JoinRequestType.NetworkAdministrator &&
                status == JoinRequestStatus.Approved &&
                request.Status != JoinRequestStatus.Approved)
            {
                await ApproveNetworkAdminRequest(request, currentUser, adminNotes);
            }
            else
            {
                request.Status = status;
                request.AdminNotes = adminNotes;
                request.UpdatedDate = DateTime.UtcNow;
                request.ProcessedByUserId = currentUser?.Id;
                request.ProcessedDate = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            _logger.LogInformation($"تم تحديث حالة طلب الانضمام #{id} إلى {status}");

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>
        /// قبول طلب مدير شبكة وإنشاء حسابه
        /// </summary>
        private async Task ApproveNetworkAdminRequest(JoinRequest request, ApplicationUser? currentUser, string? adminNotesFromForm)
        {
            try
            {
                // استخراج اسم المستخدم من Notes مع توليد كلمة مرور مؤقتة إذا لم يُدخلها المدير في نفس الطلب.
                string userName = "";

                if (!string.IsNullOrEmpty(request.Notes))
                {
                    string[] notesParts = request.Notes.Split(new[] { "اسم المستخدم المطلوب: " }, StringSplitOptions.None);
                    if (notesParts.Length > 1)
                    {
                        userName = notesParts[1].Trim();
                    }
                }

                string password = "";
                bool generatedTemporaryPassword = false;
                if (!string.IsNullOrWhiteSpace(adminNotesFromForm))
                {
                    string[] adminNotesParts = adminNotesFromForm.Split(new[] { "كلمة المرور المطلوبة: " }, StringSplitOptions.None);
                    if (adminNotesParts.Length > 1)
                    {
                        password = adminNotesParts[1].Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(password))
                {
                    password = GenerateTemporaryPassword();
                    generatedTemporaryPassword = true;
                    TempData["GeneratedTempPassword"] = password;
                    TempData["Info"] = "تم إنشاء كلمة مرور مؤقتة. شاركها مع مقدم الطلب عبر قناة آمنة.";
                }

                if (string.IsNullOrEmpty(userName))
                {
                    throw new Exception("لم يتم العثور على اسم المستخدم في الطلب");
                }

                // التحقق من عدم وجود مستخدم بنفس الاسم أو البريد الإلكتروني
                ApplicationUser? existingUser = await _userManager.FindByNameAsync(userName) ??
                                   await _userManager.FindByEmailAsync(request.Email);

                if (existingUser != null)
                {
                    throw new Exception($"يوجد بالفعل مستخدم بهذا الاسم أو البريد الإلكتروني: {request.Email}");
                }

                // إنشاء حساب المستخدم
                ApplicationUser user = new ApplicationUser
                {
                    UserName = userName,
                    Email = request.Email,
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
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

                // إضافة دور NetworkAdministrator
                await _userManager.AddToRoleAsync(user, RoleNames.NetworkAdministrator);

                // تحديث حالة الطلب
                request.Status = JoinRequestStatus.Approved;
                request.UpdatedDate = DateTime.UtcNow;
                request.ProcessedByUserId = currentUser?.Id;
                request.ProcessedDate = DateTime.UtcNow;
                request.AdminNotes = $"تم إنشاء الحساب بنجاح. اسم المستخدم: {userName}";

                await _context.SaveChangesAsync();

                _logger.LogInformation($"تم قبول طلب مدير شبكة وإنشاء الحساب: {userName} ({request.Email})");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"خطأ في قبول طلب مدير شبكة #{request.Id}");
                throw;
            }
        }

        private static string GenerateTemporaryPassword()
        {
            return $"Rt{Guid.NewGuid():N}"[..12] + "!";
        }

        /// <summary>
        /// حذف طلب انضمام
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> Delete(int id)
        {
            JoinRequest? request = await _context.JoinRequests.FindAsync(id);
            if (request == null)
            {
                return NotFound();
            }

            _context.JoinRequests.Remove(request);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"تم حذف طلب الانضمام #{id}");

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// عرض طلبات استعادة كلمة المرور
        /// </summary>
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> PasswordResets(PasswordResetStatus? status = null)
        {
            IQueryable<PasswordResetRequest> query = _context.PasswordResetRequests
                .Include(p => p.User)
                .Include(p => p.ProcessedByUser)
                .Where(p => p.ResetMethod == PasswordResetMethod.AdminRequest)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(p => p.Status == status.Value);
            }

            List<PasswordResetRequest> requests = await query
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            ViewBag.SelectedStatus = status;
            ViewBag.PendingCount = await _context.PasswordResetRequests
                .CountAsync(p => p.ResetMethod == PasswordResetMethod.AdminRequest && p.Status == PasswordResetStatus.Pending);

            return View(requests);
        }

        /// <summary>
        /// عرض تفاصيل طلب استعادة كلمة مرور
        /// </summary>
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> PasswordResetDetails(int id)
        {
            PasswordResetRequest? request = await _context.PasswordResetRequests
                .Include(p => p.User)
                .Include(p => p.ProcessedByUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (request == null)
            {
                return NotFound();
            }

            return View(request);
        }

        /// <summary>
        /// إعادة تعيين كلمة مرور المستخدم
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> ResetUserPassword(int requestId, string newPassword)
        {
            PasswordResetRequest? request = await _context.PasswordResetRequests
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == requestId);

            if (request == null || request.User == null)
            {
                return NotFound();
            }

            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                TempData["Error"] = AppMessages.PasswordMinLength;
                return RedirectToAction(nameof(PasswordResetDetails), new { id = requestId });
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            // إعادة تعيين كلمة المرور
            string token = await _userManager.GeneratePasswordResetTokenAsync(request.User);
            IdentityResult result = await _userManager.ResetPasswordAsync(request.User, token, newPassword);

            if (result.Succeeded)
            {
                request.Status = PasswordResetStatus.Completed;
                request.ProcessedDate = DateTime.Now;
                request.ProcessedByUserId = currentUser?.Id;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"تم إعادة تعيين كلمة مرور المستخدم {request.User.Email} بواسطة {currentUser?.UserName}");

                TempData["Success"] = AppMessages.OperationSuccess;
            }
            else
            {
                TempData["Error"] = "فشل في إعادة تعيين كلمة المرور: " + string.Join(", ", result.Errors.Select(e => e.Description));
            }

            return RedirectToAction(nameof(PasswordResets));
        }

        /// <summary>
        /// رفض طلب استعادة كلمة المرور
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RejectPasswordReset(int requestId, string? notes)
        {
            PasswordResetRequest? request = await _context.PasswordResetRequests.FindAsync(requestId);
            if (request == null)
            {
                return NotFound();
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            request.Status = PasswordResetStatus.Cancelled;
            request.Notes = notes;
            request.ProcessedDate = DateTime.Now;
            request.ProcessedByUserId = currentUser?.Id;
            await _context.SaveChangesAsync();

            _logger.LogInformation($"تم رفض طلب استعادة كلمة المرور #{requestId}");

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(PasswordResets));
        }
    }
}
