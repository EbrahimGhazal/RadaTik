using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.ViewModels.CollectionPoints;

namespace RadTik.Areas.CompanyAdmin.Controllers
{
    [Area("CompanyAdmin")]
    [Authorize(Roles = RoleNames.NetworkAdministrator)]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.CollectionPoints)]
    public class CollectionPointsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CollectionPointsController> _logger;

        public CollectionPointsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CollectionPointsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToRoute("networkManager-network");
            }

            // جلب مستخدمي نقطة التحصيل ضمن نفس الشبكة
            var pointUsers = await _userManager.GetUsersInRoleAsync(RoleNames.CollectionPoint);
            var pointUserIds = pointUsers
                .Where(u => u.NetworkId == networkId.Value)
                .Select(u => u.Id)
                .ToList();

            // جلب الحسابات (وإنشاء مفقود منها)
            var accounts = await _context.CollectionPointAccounts
                .Where(a => a.NetworkId == networkId.Value)
                .Include(a => a.User)
                .OrderByDescending(a => a.Balance)
                .ToListAsync();

            // أنشئ حسابات مفقودة لمستخدمي نقاط التحصيل
            var existingAccountUserIds = accounts.Select(a => a.UserId).ToHashSet();
            var missingUserIds = pointUserIds.Where(id => !existingAccountUserIds.Contains(id)).ToList();
            if (missingUserIds.Count > 0)
            {
                foreach (var userId in missingUserIds)
                {
                    _context.CollectionPointAccounts.Add(new CollectionPointAccount
                    {
                        UserId = userId,
                        NetworkId = networkId.Value,
                        Balance = 0m,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    });
                }
                await _context.SaveChangesAsync();

                accounts = await _context.CollectionPointAccounts
                    .Where(a => a.NetworkId == networkId.Value)
                    .Include(a => a.User)
                    .OrderByDescending(a => a.Balance)
                    .ToListAsync();
            }

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, currentUser, _userManager);
            ViewBag.CurrentNetworkId = networkId.Value;
            return View(accounts);
        }

        // GET: CollectionPoints/Create
        public async Task<IActionResult> Create()
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var model = new CreateCollectionPointViewModel();
            return View(model);
        }

        // POST: CollectionPoints/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateCollectionPointViewModel model)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.InitialBalance < 0)
            {
                ModelState.AddModelError(nameof(model.InitialBalance), "لا يُسمح بإدخال رصيد ابتدائي سالب.");
                return View(model);
            }

            try
            {
                var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                if (selectedNetwork is null)
                {
                    ModelState.AddModelError(string.Empty, "تعذر العثور على الشبكة المحددة.");
                    return View(model);
                }

                var requestedUserName = (model.UserName ?? string.Empty).Trim();
                var requestedEmail = (model.Email ?? string.Empty).Trim();
                var requestedPhone = (model.PhoneNumber ?? string.Empty).Trim();
                var requestedPhoneDigits = new string(requestedPhone.Where(char.IsDigit).ToArray());

                model.UserName = requestedUserName;
                model.Email = requestedEmail;
                model.PhoneNumber = requestedPhone;

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

                var pendingOrReviewRequests = await _context.JoinRequests
                    .Where(r =>
                        r.RequestType == JoinRequestType.CollectionPoint &&
                        r.Status != JoinRequestStatus.Rejected)
                    .ToListAsync();

                if (pendingOrReviewRequests.Any(r => string.Equals((r.Email ?? string.Empty).Trim(), requestedEmail, StringComparison.OrdinalIgnoreCase)))
                {
                    ModelState.AddModelError(nameof(model.Email), "يوجد طلب سابق لنقطة تحصيل بنفس البريد الإلكتروني.");
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
                        ModelState.AddModelError(nameof(model.PhoneNumber), "يوجد طلب سابق لنقطة تحصيل بنفس رقم الجوال.");
                    }
                }

                if (!string.IsNullOrWhiteSpace(requestedUserName))
                {
                    var hasDuplicateUserNameRequest = pendingOrReviewRequests.Any(r =>
                        string.Equals(ExtractValue(r.Notes, "اسم المستخدم المطلوب:"), requestedUserName, StringComparison.OrdinalIgnoreCase));

                    if (hasDuplicateUserNameRequest)
                    {
                        ModelState.AddModelError(nameof(model.UserName), "يوجد طلب سابق لنقطة تحصيل بنفس اسم المستخدم.");
                    }
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }

                var notes =
                    $"اسم المستخدم المطلوب: {requestedUserName}\n" +
                    $"معرّف الشبكة: {networkId.Value}\n" +
                    $"الرصيد الابتدائي المطلوب: {model.InitialBalance}";

                var request = new JoinRequest
                {
                    RequestType = JoinRequestType.CollectionPoint,
                    FullName = model.FullName?.Trim() ?? "نقطة تحصيل",
                    Email = requestedEmail,
                    PhoneNumber = requestedPhone,
                    Address = model.Address?.Trim(),
                    Notes = notes,
                    RequestedPassword = model.Password?.Trim(),
                    Status = JoinRequestStatus.Pending,
                    CreatedDate = DateTime.UtcNow
                };

                _context.JoinRequests.Add(request);
                await _context.SaveChangesAsync();

                TempData["Success"] = "تم إرسال طلب إنشاء نقطة التحصيل إلى مدير النظام، وسيتم إنشاء الحساب بعد الموافقة.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ أثناء إرسال طلب إنشاء نقطة تحصيل");
                ModelState.AddModelError(string.Empty, "حدث خطأ غير متوقع أثناء إرسال الطلب.");
                return View(model);
            }
        }

        private static string ExtractValue(string? source, string label)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            var idx = source.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return string.Empty;
            }

            var value = source[(idx + label.Length)..].Trim();
            var lineBreak = value.IndexOfAny(new[] { '\r', '\n' });
            if (lineBreak >= 0)
            {
                value = value[..lineBreak].Trim();
            }

            return value;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetBalance(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var account = await _context.CollectionPointAccounts
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == id && a.NetworkId == networkId.Value);

                if (account == null)
                {
                    return NotFound();
                }

                account.Balance = 0m;
                account.UpdatedAt = DateTime.Now;
                _context.Update(account);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"تم تصفير رصيد نقطة التحصيل: {account.User?.UserName}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ أثناء تصفير رصيد نقطة تحصيل {AccountId}", id);
                TempData["Error"] = $"حدث خطأ أثناء التصفير: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: CollectionPoints/Edit/5 - تعديل رصيد نقطة تحصيل (المبلغ فقط)
        public async Task<IActionResult> Edit(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.NetworkId == networkId.Value);

            if (account == null)
            {
                return NotFound();
            }

            var model = new EditCollectionPointViewModel
            {
                Id = account.Id,
                UserId = account.UserId,
                UserName = account.User?.UserName ?? account.UserId,
                CurrentBalance = account.Balance,
                NewBalance = account.Balance
            };

            return View(model);
        }

        // POST: CollectionPoints/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditCollectionPointViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (model.NewBalance < 0)
            {
                ModelState.AddModelError(nameof(model.NewBalance), "لا يُسمح بإدخال رصيد سالب.");
                return View(model);
            }

            try
            {
                var account = await _context.CollectionPointAccounts
                    .Include(a => a.User)
                    .FirstOrDefaultAsync(a => a.Id == id && a.NetworkId == networkId.Value);

                if (account == null)
                {
                    return NotFound();
                }

                account.Balance = model.NewBalance;
                account.UpdatedAt = DateTime.Now;

                _context.Update(account);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"تم تحديث رصيد نقطة التحصيل: {account.User?.UserName}";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تعديل رصيد نقطة تحصيل {AccountId}", id);
                ModelState.AddModelError(string.Empty, $"حدث خطأ أثناء حفظ التعديلات: {ex.Message}");
                return View(model);
            }
        }

        // GET: CollectionPoints/Details/5 - تفاصيل عمليات التحصيل لنقطة تحصيل
        public async Task<IActionResult> Details(int id)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرىجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.NetworkId == networkId.Value);

            if (account == null)
            {
                return NotFound();
            }

            var transactions = await _context.PaymentTransactions
                .Include(t => t.Client)
                .Where(t => t.ReceivedByUserId == account.UserId && t.NetworkId == networkId.Value)
                .OrderByDescending(t => t.PaymentDate)
                .ToListAsync();

            var model = new CollectionPointDetailsViewModel
            {
                AccountId = account.Id,
                UserId = account.UserId,
                UserName = account.User?.UserName ?? account.UserId,
                CurrentBalance = account.Balance,
                Transactions = transactions
            };

            return View(model);
        }

        /// <summary>
        /// طلبات تغذية رصيد نقاط التحصيل - مراجعة وموافقة/رفض
        /// </summary>
        public async Task<IActionResult> TopUpRequests(CollectionPointTopUpStatus? status = null)
        {
            ViewData["Title"] = "طلبات تغذية رصيد نقاط التحصيل";

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            // عرض الطلبات الموجهة لهذه الشبكة (مدير الشركة) - طلبات مدير الشركة فقط
            var query = _context.CollectionPointTopUpRequests
                .Include(r => r.CollectionPointAccount)
                    .ThenInclude(a => a!.User)
                .Include(r => r.PaymentMethod)
                .Include(r => r.RequestedByUser)
                .Include(r => r.TargetNetwork)
                .Where(r => r.CollectionPointAccount != null &&
                    r.RequestTargetType == CollectionPointTopUpTarget.CompanyManager &&
                    (r.TargetNetworkId == networkId.Value ||
                     (r.TargetNetworkId == null && r.CollectionPointAccount.NetworkId == networkId.Value)))
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            var list = await query.OrderByDescending(r => r.RequestedAt).Take(200).ToListAsync();
            ViewBag.Items = list;
            ViewBag.SelectedStatus = status;
            ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
                .CountAsync(r => r.CollectionPointAccount != null &&
                    r.RequestTargetType == CollectionPointTopUpTarget.CompanyManager &&
                    (r.TargetNetworkId == networkId.Value || (r.TargetNetworkId == null && r.CollectionPointAccount.NetworkId == networkId.Value)) &&
                    r.Status == CollectionPointTopUpStatus.Pending);

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveTopUpRequest(int id, string? adminNotes = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var req = await _context.CollectionPointTopUpRequests
                .Include(r => r.CollectionPointAccount).ThenInclude(a => a!.User)
                .Include(r => r.TargetNetwork)
                .FirstOrDefaultAsync(r => r.Id == id);

            var targetNetId = req?.TargetNetworkId ?? req?.CollectionPointAccount?.NetworkId;
            if (req == null || !targetNetId.HasValue || targetNetId.Value != networkId.Value)
            {
                return NotFound();
            }

            if (req.Status != CollectionPointTopUpStatus.Pending)
            {
                TempData["Error"] = "لا يمكن الموافقة على طلب غير معلّق.";
                return RedirectToAction(nameof(TopUpRequests));
            }

            if (string.IsNullOrWhiteSpace(req.ReferenceNumber) || string.IsNullOrWhiteSpace(req.ReceiptImagePath))
            {
                TempData["Error"] = "لا يمكن الموافقة: الطلب ناقص بيانات الإيصال (رقم المرجع + صورة الإيصال).";
                return RedirectToAction(nameof(TopUpRequests));
            }

            // حسم المبلغ من محفظة الشبكة (مدير الشركة)
            var network = await _context.Networks.FindAsync(targetNetId.Value);
            if (network == null)
            {
                TempData["Error"] = "لم يتم العثور على شبكة.";
                return RedirectToAction(nameof(TopUpRequests));
            }
            if (network.Balance < req.Amount)
            {
                TempData["Error"] = $"رصيد الشبكة غير كافٍ. الرصيد الحالي: {network.Balance:N0} ل.س والمطلوب: {req.Amount:N0} ل.س.";
                return RedirectToAction(nameof(TopUpRequests));
            }

            var account = req.CollectionPointAccount!;
            account.Balance += req.Amount;
            account.UpdatedAt = DateTime.Now;

            var prevBalance = network.Balance;
            network.Balance -= req.Amount;

            req.Status = CollectionPointTopUpStatus.Approved;
            req.ProcessedByUserId = currentUser.Id;
            req.ProcessedAt = DateTime.Now;
            req.AdminNotes = adminNotes?.Trim();

            var walletTx = new NetworkWalletTransaction
            {
                NetworkId = network.Id,
                Type = NetworkWalletTransactionType.Adjustment,
                SignedAmount = -req.Amount,
                PreviousBalance = prevBalance,
                NewBalance = network.Balance,
                CreatedByUserId = currentUser.Id,
                CreatedAt = DateTime.Now,
                Notes = $"تغذية رصيد نقطة التحصيل #{req.Id}"
            };
            _context.NetworkWalletTransactions.Add(walletTx);

            await _context.SaveChangesAsync();

            _logger.LogInformation("تمت الموافقة على طلب تغذية رصيد #{Id} لنقطة التحصيل {UserName} بمبلغ {Amount}",
                id, account.User?.UserName, req.Amount);

            TempData["Success"] = $"تمت الموافقة على الطلب وإضافة {req.Amount:N0} ل.س إلى رصيد نقطة التحصيل.";
            return RedirectToAction(nameof(TopUpRequests));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectTopUpRequest(int id, string? adminNotes = null)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var req = await _context.CollectionPointTopUpRequests
                .Include(r => r.CollectionPointAccount)
                .FirstOrDefaultAsync(r => r.Id == id);

            var reqTargetNetId = req?.TargetNetworkId ?? req?.CollectionPointAccount?.NetworkId;
            if (req == null || !reqTargetNetId.HasValue || reqTargetNetId.Value != networkId.Value)
            {
                return NotFound();
            }

            if (req.Status != CollectionPointTopUpStatus.Pending)
            {
                TempData["Error"] = "لا يمكن رفض طلب غير معلّق.";
                return RedirectToAction(nameof(TopUpRequests));
            }

            req.Status = CollectionPointTopUpStatus.Rejected;
            req.ProcessedByUserId = currentUser.Id;
            req.ProcessedAt = DateTime.Now;
            req.AdminNotes = adminNotes?.Trim();

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم رفض الطلب.";
            return RedirectToAction(nameof(TopUpRequests));
        }
    }
}

