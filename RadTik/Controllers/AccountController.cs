using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Middleware;
using RadTik.ViewModels.Account;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;

namespace RadTik.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AccountController> _logger;
        private readonly IConfiguration _configuration;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            ILogger<AccountController> logger,
            IConfiguration configuration)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null)
            {
                user = await _userManager.FindByEmailAsync(model.UserName);
            }

            // إذا لم يكن المستخدم موجوداً، تحقّق من وجود طلب مدير شركة (قيد الانتظار/مرفوض)
            if (user == null)
            {
                var pendingOrRejectedRequest = await _context.JoinRequests
                    .Where(r => r.RequestType == JoinRequestType.NetworkAdministrator)
                    .OrderByDescending(r => r.CreatedDate)
                    .FirstOrDefaultAsync(r =>
                        r.Email == model.UserName ||
                        (r.Notes != null && r.Notes.Contains(model.UserName)));

                if (pendingOrRejectedRequest != null)
                {
                    if (pendingOrRejectedRequest.Status == JoinRequestStatus.Pending || pendingOrRejectedRequest.Status == JoinRequestStatus.UnderReview)
                    {
                        ModelState.AddModelError(string.Empty, "طلب إنشاء حساب مدير الشركة قيد الانتظار. يرجى الانتظار حتى تتم الموافقة.");
                        return View(model);
                    }

                    if (pendingOrRejectedRequest.Status == JoinRequestStatus.Rejected)
                    {
                        var reason = string.IsNullOrWhiteSpace(pendingOrRejectedRequest.AdminNotes) ? "لم يتم تحديد السبب." : pendingOrRejectedRequest.AdminNotes;
                        ModelState.AddModelError(string.Empty, $"تم رفض طلب إنشاء حساب مدير الشركة. السبب: {reason}");
                        return View(model);
                    }
                }

                ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
                return View(model);
            }

            // حساب المستخدم مجمد: منع الدخول لغير المشتركين فقط (المشترك يمكنه الدخول حتى لو كان اشتراكه متوقفاً)
            if (!user.IsActive && !user.ClientId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "تم تجميد حسابك من قبل إدارة النظام. يرجى التواصل مع الدعم.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                user.UserName!,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation($"المستخدم {user.UserName} سجل دخول بنجاح");
                var roles = await _userManager.GetRolesAsync(user);

                HttpContext.Session.Remove(AreaIsolationMiddleware.SessionKeyActiveArea);

                // تحديد الوجهة ثم تعيين المنطقة النشطة بنفس الوجهة (لتجنب منع الوصول عند تعدد الأدوار)
                string? activeArea = null;
                IActionResult? redirect = null;

                if (roles.Contains(RoleNames.SystemAdministrator))
                {
                    activeArea = "SystemAdmin";
                    redirect = RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin", tab = "dashboard" });
                }
                else if (roles.Contains(RoleNames.CollectionPoint))
                {
                    activeArea = "CollectionPoint";
                    redirect = Redirect("/collectionPoint/dashboard");
                }
                else if (roles.Contains(RoleNames.Client))
                {
                    activeArea = "ClientPortal";
                    redirect = Redirect("/clientPortal/dashboard");
                }
                else if (roles.Contains(RoleNames.CompanyEmployee) || roles.Contains(RoleNames.EmployeeLegacy))
                {
                    activeArea = "CompanyEmployee";
                    redirect = RedirectToRoute("employee-dashboard");
                }
                else if (roles.Contains(RoleNames.NetworkAdministrator))
                {
                    activeArea = "CompanyAdmin";
                    redirect = RedirectToRoute("networkManager-dashboard");
                }

                if (!string.IsNullOrWhiteSpace(activeArea))
                {
                    HttpContext.Session.SetString(AreaIsolationMiddleware.SessionKeyActiveArea, activeArea);
                }

                if (redirect != null)
                {
                    return redirect;
                }
                return RedirectToLocal(returnUrl);
            }

            ModelState.AddModelError(string.Empty, "اسم المستخدم أو كلمة المرور غير صحيحة");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            HttpContext.Session.Remove(AreaIsolationMiddleware.SessionKeyActiveArea);
            _logger.LogInformation("تم تسجيل الخروج");
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [Authorize(Roles = RoleNames.NetworkAdministrator)]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [Authorize(Roles = RoleNames.NetworkAdministrator)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var user = new ApplicationUser
            {
                UserName = model.UserName,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.PhoneNumber,
                CreatedDate = DateTime.UtcNow,
                IsActive = true,
                NetworkId = networkId.Value
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                // إضافة الأدوار المحددة
                if (model.Roles != null && model.Roles.Count > 0)
                {
                    foreach (var role in model.Roles)
                    {
                        if (await _userManager.IsInRoleAsync(user, role) == false)
                        {
                            await _userManager.AddToRoleAsync(user, role);
                        }
                    }
                }

                // إذا كان المستخدم نقطة تحصيل مالي، أنشئ حساب نقطة التحصيل تلقائياً
                if (model.Roles != null && model.Roles.Contains(RoleNames.CollectionPoint))
                {
                    var existingAccount = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
                    if (existingAccount == null)
                    {
                        _context.CollectionPointAccounts.Add(new CollectionPointAccount
                        {
                            UserId = user.Id,
                            NetworkId = networkId.Value,
                            Balance = 0m,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        });
                        await _context.SaveChangesAsync();
                    }
                }

                _logger.LogInformation($"تم إنشاء حساب جديد للمستخدم {user.UserName}");
                TempData["Success"] = "تم إنشاء الحساب بنجاح";
                return RedirectToAction("Index", "Admin");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult RegisterNetworkAdmin()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterNetworkAdmin(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var requestedUserName = (model.UserName ?? string.Empty).Trim();
            var requestedEmail = (model.Email ?? string.Empty).Trim();
            var requestedPhone = (model.PhoneNumber ?? string.Empty).Trim();
            var requestedPhoneDigits = new string(requestedPhone.Where(char.IsDigit).ToArray());

            model.UserName = requestedUserName;
            model.Email = requestedEmail;
            model.PhoneNumber = requestedPhone;

            // منع التكرار على حسابات المستخدمين الحالية
            if (!string.IsNullOrWhiteSpace(requestedUserName))
            {
                var existingUserByUserName = await _userManager.FindByNameAsync(requestedUserName);
                if (existingUserByUserName != null)
                {
                    ModelState.AddModelError(nameof(model.UserName), "اسم المستخدم موجود مسبقاً. يرجى اختيار اسم مستخدم آخر.");
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedEmail))
            {
                var existingUserByEmail = await _userManager.FindByEmailAsync(requestedEmail);
                if (existingUserByEmail != null)
                {
                    ModelState.AddModelError(nameof(model.Email), "البريد الإلكتروني موجود مسبقاً. يرجى استخدام بريد إلكتروني آخر.");
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedPhone))
            {
                var existingUserPhoneNumbers = await _userManager.Users
                    .Where(u => !string.IsNullOrEmpty(u.PhoneNumber))
                    .Select(u => u.PhoneNumber!)
                    .ToListAsync();

                var hasDuplicatePhone = existingUserPhoneNumbers.Any(phone =>
                {
                    var normalized = phone.Trim();
                    if (string.Equals(normalized, requestedPhone, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var digits = new string(normalized.Where(char.IsDigit).ToArray());
                    return !string.IsNullOrWhiteSpace(requestedPhoneDigits) &&
                           !string.IsNullOrWhiteSpace(digits) &&
                           digits == requestedPhoneDigits;
                });

                if (hasDuplicatePhone)
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), "رقم الجوال موجود مسبقاً. يرجى إدخال رقم جوال آخر.");
                }
            }

            // منع التكرار على طلبات مدير الشركة المعلقة/قيد المراجعة
            var pendingOrReviewRequests = await _context.JoinRequests
                .Where(r => r.RequestType == JoinRequestType.NetworkAdministrator &&
                            (r.Status == JoinRequestStatus.Pending || r.Status == JoinRequestStatus.UnderReview))
                .ToListAsync();

            if (pendingOrReviewRequests.Any(r => string.Equals((r.Email ?? string.Empty).Trim(), requestedEmail, StringComparison.OrdinalIgnoreCase)))
            {
                ModelState.AddModelError(nameof(model.Email), "يوجد طلب سابق بهذا البريد الإلكتروني. يرجى تعديل البريد الإلكتروني.");
            }

            if (!string.IsNullOrWhiteSpace(requestedPhone))
            {
                var hasDuplicatePhoneRequest = pendingOrReviewRequests.Any(r =>
                {
                    var phone = (r.PhoneNumber ?? string.Empty).Trim();
                    if (string.Equals(phone, requestedPhone, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    var digits = new string(phone.Where(char.IsDigit).ToArray());
                    return !string.IsNullOrWhiteSpace(requestedPhoneDigits) &&
                           !string.IsNullOrWhiteSpace(digits) &&
                           digits == requestedPhoneDigits;
                });

                if (hasDuplicatePhoneRequest)
                {
                    ModelState.AddModelError(nameof(model.PhoneNumber), "يوجد طلب سابق بهذا الرقم. يرجى تعديل رقم الجوال.");
                }
            }

            if (!string.IsNullOrWhiteSpace(requestedUserName))
            {
                var usernameMarker = $"اسم المستخدم المطلوب: {requestedUserName}";
                var hasDuplicateUserNameRequest = pendingOrReviewRequests.Any(r =>
                    !string.IsNullOrWhiteSpace(r.Notes) &&
                    r.Notes.Contains(usernameMarker, StringComparison.OrdinalIgnoreCase));

                if (hasDuplicateUserNameRequest)
                {
                    ModelState.AddModelError(nameof(model.UserName), "يوجد طلب سابق بنفس اسم المستخدم. يرجى اختيار اسم مستخدم آخر.");
                }
            }

            if (!ModelState.IsValid)
            {
                ModelState.AddModelError(string.Empty, "يوجد تكرار في بعض البيانات المدخلة. يرجى تعديل الحقول المكررة ثم إعادة الإرسال.");
                return View(model);
            }

            // إنشاء طلب انضمام بدلاً من حساب مباشر
            var joinRequest = new JoinRequest
            {
                RequestType = JoinRequestType.NetworkAdministrator,
                FullName = model.FullName ?? "مدير الشبكة",
                Email = requestedEmail,
                PhoneNumber = requestedPhone,
                Notes = $"اسم المستخدم المطلوب: {requestedUserName}",
                RequestedPassword = model.Password?.Trim(),
                Status = JoinRequestStatus.Pending,
                CreatedDate = DateTime.UtcNow
            };
            joinRequest.AdminNotes = "تم حفظ كلمة المرور المطلوبة بشكل مشفر.";

            _context.JoinRequests.Add(joinRequest);
            await _context.SaveChangesAsync();

            _logger.LogInformation($"تم إنشاء طلب انضمام لمدير شبكة جديد: {model.Email}");
            TempData["Success"] = "تم إرسال طلبك بنجاح! سيتم مراجعة طلبك من قبل الإدارة. سيتم إرسال رسالة بريد إلكتروني عند الموافقة على طلبك.";
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }

        /// <summary>
        /// صفحة نسيت كلمة المرور
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        /// <summary>
        /// معالجة طلب استعادة كلمة المرور
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // البحث عن المستخدم بالبريد الإلكتروني
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null)
            {
                // لا نكشف عدم وجود المستخدم لأسباب أمنية
                TempData["Info"] = "إذا كان البريد الإلكتروني مسجلاً، سيتم إرسال تعليمات استعادة كلمة المرور";
                return View("ForgotPasswordConfirmation");
            }

            // التحقق من نوع المستخدم (يجب أن يكون مدير أو موظف)
            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains(RoleNames.NetworkAdministrator) &&
                !roles.Contains(RoleNames.CompanyEmployee) &&
                !roles.Contains(RoleNames.EmployeeLegacy) &&
                !roles.Contains(RoleNames.SystemEmployee))
            {
                // العملاء يستخدمون كلمة مرور افتراضية ويجب تغييرها من لوحة التحكم
                ModelState.AddModelError("", "العملاء يمكنهم إعادة تعيين كلمة المرور من خلال التواصل مع الدعم الفني");
                return View(model);
            }

            if (model.ResetMethod == PasswordResetMethod.Email)
            {
                // إنشاء رمز تحقق عشوائي من 6 أرقام
                var verificationCode = new Random().Next(100000, 999999).ToString();
                
                var resetRequest = new PasswordResetRequest
                {
                    UserId = user.Id,
                    Email = model.Email,
                    ResetMethod = PasswordResetMethod.Email,
                    VerificationCode = verificationCode,
                    CodeExpiryDate = DateTime.UtcNow.AddMinutes(30),
                    Status = PasswordResetStatus.CodeSent,
                    CreatedDate = DateTime.UtcNow
                };

                _context.PasswordResetRequests.Add(resetRequest);
                await _context.SaveChangesAsync();

                var sent = await SendPasswordResetCodeEmailAsync(model.Email, verificationCode);
                if (!sent)
                {
                    // Fallback for development or missing SMTP configuration
                    _logger.LogInformation($"رمز التحقق للمستخدم {user.Email}: {verificationCode}");
                }

                TempData["ResetRequestId"] = resetRequest.Id;
                TempData["Info"] = sent
                    ? "تم إرسال رمز التحقق إلى بريدك الإلكتروني."
                    : $"تعذر إرسال البريد حاليا. (للتطوير: رمز التحقق هو {verificationCode})";
                return RedirectToAction(nameof(VerifyResetCode));
            }
            else // طلب لمدير النظام
            {
                var resetRequest = new PasswordResetRequest
                {
                    UserId = user.Id,
                    Email = model.Email,
                    ResetMethod = PasswordResetMethod.AdminRequest,
                    Status = PasswordResetStatus.Pending,
                    Notes = model.Notes,
                    CreatedDate = DateTime.UtcNow
                };

                _context.PasswordResetRequests.Add(resetRequest);
                int v = await _context.SaveChangesAsync();

                _logger.LogInformation($"طلب استعادة كلمة مرور جديد من المستخدم {user.Email} (طلب للمدير)");

                TempData["Success"] = "تم إرسال طلبك لمدير النظام. سيتم التواصل معك قريباً.";
                return View("ForgotPasswordConfirmation");
            }
        }

        /// <summary>
        /// صفحة إدخال رمز التحقق
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult VerifyResetCode()
        {
            if (TempData["ResetRequestId"] == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            TempData.Keep("ResetRequestId");
            return View();
        }

        /// <summary>
        /// التحقق من رمز الاستعادة
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyResetCode(VerifyCodeViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var requestId = TempData["ResetRequestId"] as int?;
            if (requestId == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            var resetRequest = await _context.PasswordResetRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == requestId);

            if (resetRequest == null)
            {
                ModelState.AddModelError("", "طلب غير صالح");
                return View(model);
            }

            if (resetRequest.CodeExpiryDate < DateTime.UtcNow)
            {
                resetRequest.Status = PasswordResetStatus.Expired;
                await _context.SaveChangesAsync();
                ModelState.AddModelError("", "انتهت صلاحية رمز التحقق. يرجى طلب رمز جديد.");
                return View(model);
            }

            if (resetRequest.VerificationCode != model.Code)
            {
                ModelState.AddModelError("", "رمز التحقق غير صحيح");
                TempData["ResetRequestId"] = requestId;
                return View(model);
            }

            resetRequest.Status = PasswordResetStatus.Verified;
            await _context.SaveChangesAsync();

            TempData["ResetRequestId"] = requestId;
            return RedirectToAction(nameof(ResetPassword));
        }

        /// <summary>
        /// صفحة إعادة تعيين كلمة المرور
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword()
        {
            if (TempData["ResetRequestId"] == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            TempData.Keep("ResetRequestId");
            return View();
        }

        /// <summary>
        /// معالجة إعادة تعيين كلمة المرور
        /// </summary>
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var requestId = TempData["ResetRequestId"] as int?;
            if (requestId == null)
            {
                return RedirectToAction(nameof(ForgotPassword));
            }

            var resetRequest = await _context.PasswordResetRequests
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == requestId && r.Status == PasswordResetStatus.Verified);

            if (resetRequest == null || resetRequest.User == null)
            {
                ModelState.AddModelError("", "طلب غير صالح");
                return View(model);
            }

            // إعادة تعيين كلمة المرور
            var token = await _userManager.GeneratePasswordResetTokenAsync(resetRequest.User);
            var result = await _userManager.ResetPasswordAsync(resetRequest.User, token, model.NewPassword);

            if (result.Succeeded)
            {
                resetRequest.Status = PasswordResetStatus.Completed;
                resetRequest.ProcessedDate = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation($"تم إعادة تعيين كلمة المرور للمستخدم {resetRequest.User.Email}");

                TempData["Success"] = "تم تغيير كلمة المرور بنجاح. يمكنك الآن تسجيل الدخول.";
                return RedirectToAction(nameof(Login));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            TempData["ResetRequestId"] = requestId;
            return View(model);
        }

        /// <summary>
        /// تأكيد طلب استعادة كلمة المرور
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return View();
        }

        private IActionResult RedirectToLocal(string? returnUrl)
        {
            if (Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        private async Task<bool> SendPasswordResetCodeEmailAsync(string toEmail, string verificationCode)
        {
            var host = _configuration["EmailSettings:SmtpHost"];
            var fromEmail = _configuration["EmailSettings:FromEmail"];

            if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(fromEmail))
            {
                _logger.LogWarning("EmailSettings are missing. SMTP host or from email is not configured.");
                return false;
            }

            var port = int.TryParse(_configuration["EmailSettings:SmtpPort"], out var p) ? p : 587;
            var enableSsl = !string.Equals(_configuration["EmailSettings:EnableSsl"], "false", StringComparison.OrdinalIgnoreCase);
            var userName = _configuration["EmailSettings:UserName"];
            var password = _configuration["EmailSettings:Password"];
            var fromName = _configuration["EmailSettings:FromName"] ?? "RadTik";

            try
            {
                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "رمز التحقق لإعادة تعيين كلمة المرور",
                    Body = $@"
                        <div style='font-family:Tahoma,Arial,sans-serif;direction:rtl;text-align:right'>
                            <h3 style='margin:0 0 12px'>استعادة كلمة المرور</h3>
                            <p>رمز التحقق الخاص بك هو:</p>
                            <div style='font-size:28px;font-weight:700;letter-spacing:2px;background:#f3f6fb;padding:10px 14px;border-radius:8px;display:inline-block'>
                                {WebUtility.HtmlEncode(verificationCode)}
                            </div>
                            <p style='margin-top:12px;color:#666'>الرمز صالح لمدة 30 دقيقة.</p>
                        </div>",
                    IsBodyHtml = true
                };
                message.To.Add(toEmail);

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false
                };

                if (!string.IsNullOrWhiteSpace(userName))
                {
                    client.Credentials = new NetworkCredential(userName, password ?? string.Empty);
                }

                await client.SendMailAsync(message);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password reset email to {Email}", toEmail);
                return false;
            }
        }
    }
}
