using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.ViewModels.CollectionPoints;

namespace RadaTik.Areas.CompanyAdmin.Controllers
{
    [Area("CompanyAdmin")]
    [Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
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
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToRoute("networkManager-network");
            }

            // جلب مستخدمي نقطة التحصيل ضمن نفس الشبكة
            IList<ApplicationUser> pointUsers = await _userManager.GetUsersInRoleAsync(RoleNames.CollectionPoint);
            List<string> pointUserIds = pointUsers
                .Where(u => u.NetworkId == networkId.Value)
                .Select(u => u.Id)
                .ToList();

            // جلب الحسابات (وإنشاء مفقود منها)
            List<CollectionPointAccount> accounts = await _context.CollectionPointAccounts
                .Where(a => a.NetworkId == networkId.Value)
                .Include(a => a.User)
                .OrderByDescending(a => a.Balance)
                .ToListAsync();

            // أنشئ حسابات مفقودة لمستخدمي نقاط التحصيل
            HashSet<string> existingAccountUserIds = accounts.Select(a => a.UserId).ToHashSet();
            List<string> missingUserIds = pointUserIds.Where(id => !existingAccountUserIds.Contains(id)).ToList();
            if (missingUserIds.Count > 0)
            {
                foreach (string? userId in missingUserIds)
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

        // GET: CollectionPoints/Create — الطلب يُقدَّم من صفحة تسجيل الدخول العامة فقط.
        public IActionResult Create()
        {
            TempData["Info"] = "يُرجى تقديم طلب إنشاء نقطة تحصيل من صفحة تسجيل الدخول العامة عبر «طلب إنشاء نقطة تحصيل». لا يشترط وجود مدير شركة.";
            return RedirectToAction(nameof(Index));
        }

        // POST: CollectionPoints/Create — معطّل؛ استخدم التسجيل العام.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CreateCollectionPointViewModel model)
        {
            TempData["Info"] = "يُرجى تقديم طلب إنشاء نقطة تحصيل من صفحة تسجيل الدخول العامة عبر «طلب إنشاء نقطة تحصيل». لا يشترط وجود مدير شركة.";
            return RedirectToAction(nameof(Index));
        }

        private static string ExtractValue(string? source, string label)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            int idx = source.IndexOf(label, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                return string.Empty;
            }

            string value = source[(idx + label.Length)..].Trim();
            int lineBreak = value.IndexOfAny(new[] { '\r', '\n' });
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
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Index));
            }

            try
            {
                CollectionPointAccount? account = await _context.CollectionPointAccounts
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
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Index));
            }

            CollectionPointAccount? account = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.NetworkId == networkId.Value);

            if (account == null)
            {
                return NotFound();
            }

            EditCollectionPointViewModel model = new EditCollectionPointViewModel
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

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
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
                CollectionPointAccount? account = await _context.CollectionPointAccounts
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
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرىجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            CollectionPointAccount? account = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id && a.NetworkId == networkId.Value);

            if (account == null)
            {
                return NotFound();
            }

            List<PaymentTransaction> transactions = await _context.PaymentTransactions
                .Include(t => t.Client)
                .Where(t => t.ReceivedByUserId == account.UserId && t.NetworkId == networkId.Value)
                .OrderByDescending(t => t.PaymentDate)
                .ToListAsync();

            CollectionPointDetailsViewModel model = new CollectionPointDetailsViewModel
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
        /// طلبات تغذية رصيد نقاط التحصيل تُعالَج حصرياً من مدير النظام.
        /// </summary>
        public IActionResult TopUpRequests(CollectionPointTopUpStatus? status = null)
        {
            TempData["Info"] = "طلبات تغذية رصيد نقاط التحصيل تُقدَّم وتُراجع حصرياً من قبل مدير النظام.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ApproveTopUpRequest(int id, string? adminNotes = null)
        {
            TempData["Error"] = "لا يمكن معالجة طلبات تغذية نقاط التحصيل من لوحة مدير الشركة؛ يتم ذلك من لوحة مدير النظام فقط.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectTopUpRequest(int id, string? adminNotes = null)
        {
            TempData["Error"] = "لا يمكن معالجة طلبات تغذية نقاط التحصيل من لوحة مدير الشركة؛ يتم ذلك من لوحة مدير النظام فقط.";
            return RedirectToAction(nameof(Index));
        }
    }
}

