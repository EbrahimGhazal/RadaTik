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
using global::RadaTik.ViewModels.CollectionPoints;

namespace RadaTik.Areas.CompanyAdmin.Controllers
{
    [Area("CompanyAdmin")]
    [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator},{RoleNames.CompanyEmployee},{RoleNames.EmployeeLegacy}")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.CollectionPoints)]
    public class CollectionPointsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IPermissionService _permissionService;
        private readonly ILogger<CollectionPointsController> _logger;

        public CollectionPointsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IPermissionService permissionService,
            ILogger<CollectionPointsController> logger)
        {
            _context = context;
            _userManager = userManager;
            _permissionService = permissionService;
            _logger = logger;
        }

        /// <summary>
        /// تقرير تسديدات مشتركي الشركة عبر نقاط التحصيل (الصفحة الرئيسية).
        /// </summary>
        [RequirePermission("CollectionPointAccounts.View")]
        public async Task<IActionResult> Index(DateTime? from = null, DateTime? to = null, string? collectionPointUserId = null)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToRoute("networkManager-network");
            }

            IList<ApplicationUser> pointUsers = await _userManager.GetUsersInRoleAsync(RoleNames.CollectionPoint);
            HashSet<string> pointUserIds = pointUsers.Select(u => u.Id).ToHashSet(StringComparer.Ordinal);
            Dictionary<string, string> pointNames = pointUsers.ToDictionary(
                u => u.Id,
                u => string.IsNullOrWhiteSpace(u.FullName) ? (u.UserName ?? u.Id) : u.FullName!,
                StringComparer.Ordinal);

            var accountIdPairs = await _context.CollectionPointAccounts
                .AsNoTracking()
                .Where(a => pointUserIds.Contains(a.UserId))
                .GroupBy(a => a.UserId)
                .Select(g => new { UserId = g.Key, Id = g.Min(a => a.Id) })
                .ToListAsync();
            Dictionary<string, int> accountIdsByUser = accountIdPairs
                .ToDictionary(x => x.UserId, x => x.Id, StringComparer.Ordinal);

            DateTime? fromDt = from?.Date;
            DateTime? toExclusive = to?.Date.AddDays(1);
            if (fromDt.HasValue && to.HasValue && to.Value.Date < fromDt.Value)
            {
                (fromDt, toExclusive) = (to.Value.Date, fromDt.Value.AddDays(1));
                (from, to) = (to.Value.Date, from.Value.Date);
            }

            string? selectedPointUserId = string.IsNullOrWhiteSpace(collectionPointUserId)
                ? null
                : collectionPointUserId.Trim();
            if (selectedPointUserId != null && !pointUserIds.Contains(selectedPointUserId))
            {
                selectedPointUserId = null;
            }

            IQueryable<PaymentTransaction> query = _context.PaymentTransactions
                .AsNoTracking()
                .Include(t => t.Client!)
                    .ThenInclude(c => c.Profile)
                .Include(t => t.ReceivedByUser)
                .Where(t => t.NetworkId == networkId.Value
                            && t.Client != null
                            && t.Client.NetworkId == networkId.Value
                            && pointUserIds.Contains(t.ReceivedByUserId));

            if (!string.IsNullOrEmpty(selectedPointUserId))
            {
                query = query.Where(t => t.ReceivedByUserId == selectedPointUserId);
            }

            if (fromDt.HasValue)
            {
                query = query.Where(t => t.PaymentDate >= fromDt.Value);
            }

            if (toExclusive.HasValue)
            {
                query = query.Where(t => t.PaymentDate < toExclusive.Value);
            }

            bool hasFilter = fromDt.HasValue || toExclusive.HasValue || !string.IsNullOrEmpty(selectedPointUserId);
            List<PaymentTransaction> transactions = hasFilter
                ? await query.OrderByDescending(t => t.PaymentDate).Take(5000).ToListAsync()
                : await query.OrderByDescending(t => t.PaymentDate).Take(500).ToListAsync();

            List<CollectionPointPaymentReportRow> rows = new();
            decimal totalWithTax = 0m;
            decimal totalWithoutTax = 0m;
            decimal taxAmount = 0m;
            int sequence = 1;

            foreach (PaymentTransaction t in transactions)
            {
                decimal paid = t.PaymentAmount > 0 ? t.PaymentAmount : t.Amount;
                (decimal baseAmount, decimal rowTax) = SplitTax(t, paid);
                totalWithTax += paid;
                totalWithoutTax += baseAmount;
                taxAmount += rowTax;

                string pointName = pointNames.TryGetValue(t.ReceivedByUserId, out string? mappedName)
                    ? mappedName
                    : (t.ReceivedByUser?.FullName
                       ?? t.ReceivedByUser?.UserName
                       ?? t.ReceivedByUserId);
                accountIdsByUser.TryGetValue(t.ReceivedByUserId, out int accountId);

                rows.Add(new CollectionPointPaymentReportRow
                {
                    Sequence = sequence++,
                    PaymentDate = t.PaymentDate,
                    SubscriberName = t.Client?.Name ?? $"عميل رقم {t.ClientId}",
                    PhoneNumber = string.IsNullOrWhiteSpace(t.Client?.PhoneNumber) ? "—" : t.Client!.PhoneNumber!,
                    ProfileSpeed = BuildProfileSpeed(t.Client),
                    CollectionPointName = pointName,
                    CollectionPointAccountId = accountId > 0 ? accountId : null,
                    OperationType = t.OperationType ?? "ReceivePayment",
                    OperationTypeLabel = OperationTypeLabel(t.OperationType),
                    AmountPaid = paid
                });
            }

            HashSet<string> usedPointIds = transactions.Select(t => t.ReceivedByUserId).ToHashSet(StringComparer.Ordinal);
            List<CollectionPointFilterOption> pointOptions = pointUsers
                .Where(u => usedPointIds.Contains(u.Id) || accountIdsByUser.ContainsKey(u.Id))
                .OrderBy(u => pointNames[u.Id], StringComparer.OrdinalIgnoreCase)
                .Select(u => new CollectionPointFilterOption
                {
                    UserId = u.Id,
                    DisplayName = pointNames[u.Id]
                })
                .ToList();

            // إن لم تظهر نقاط من النتائج، اعرض كل نقاط التحصيل للاختيار
            if (pointOptions.Count == 0)
            {
                pointOptions = pointUsers
                    .OrderBy(u => pointNames[u.Id], StringComparer.OrdinalIgnoreCase)
                    .Select(u => new CollectionPointFilterOption
                    {
                        UserId = u.Id,
                        DisplayName = pointNames[u.Id]
                    })
                    .ToList();
            }

            CollectionPointPaymentsReportViewModel model = new()
            {
                From = from?.Date,
                To = to?.Date,
                CollectionPointUserId = selectedPointUserId,
                PrintedAt = DateTime.Now,
                TotalWithoutTax = totalWithoutTax,
                TaxAmount = taxAmount,
                TotalWithTax = totalWithTax,
                ResultCount = rows.Count,
                Rows = rows,
                CollectionPointOptions = pointOptions
            };

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, currentUser, _userManager);
            ViewBag.CurrentNetworkId = networkId.Value;
            ViewBag.CanManage = await _permissionService.HasPermissionAsync(User, "CollectionPointAccounts.Manage");
            return View(model);
        }

        /// <summary>أرصدة حسابات نقاط التحصيل التابعة للشركة.</summary>
        [RequirePermission("CollectionPointAccounts.View")]
        public async Task<IActionResult> Accounts()
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToRoute("networkManager-network");
            }

            IList<ApplicationUser> pointUsers = await _userManager.GetUsersInRoleAsync(RoleNames.CollectionPoint);
            List<string> pointUserIds = pointUsers
                .Where(u => u.NetworkId == networkId.Value)
                .Select(u => u.Id)
                .ToList();

            List<CollectionPointAccount> accounts = await _context.CollectionPointAccounts
                .Where(a => a.NetworkId == networkId.Value)
                .Include(a => a.User)
                .OrderByDescending(a => a.Balance)
                .ToListAsync();

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
            ViewBag.CanManage = await _permissionService.HasPermissionAsync(User, "CollectionPointAccounts.Manage");
            return View(accounts);
        }

        // GET: CollectionPoints/Create — الطلب يُقدَّم من صفحة تسجيل الدخول العامة فقط.
        [RequirePermission("CollectionPointAccounts.Manage")]
        public IActionResult Create()
        {
            TempData["Info"] = "يُرجى تقديم طلب إنشاء نقطة تحصيل من صفحة تسجيل الدخول العامة عبر «طلب إنشاء نقطة تحصيل». لا يشترط وجود مدير شركة.";
            return RedirectToAction(nameof(Accounts));
        }

        // POST: CollectionPoints/Create — معطّل؛ استخدم التسجيل العام.
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("CollectionPointAccounts.Manage")]
        public IActionResult Create(CreateCollectionPointViewModel model)
        {
            TempData["Info"] = "يُرجى تقديم طلب إنشاء نقطة تحصيل من صفحة تسجيل الدخول العامة عبر «طلب إنشاء نقطة تحصيل». لا يشترط وجود مدير شركة.";
            return RedirectToAction(nameof(Accounts));
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
        [RequirePermission("CollectionPointAccounts.Manage")]
        public async Task<IActionResult> ResetBalance(int id)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Accounts));
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

            return RedirectToAction(nameof(Accounts));
        }

        // GET: CollectionPoints/Edit/5 - تعديل رصيد نقطة تحصيل (المبلغ فقط)
        [RequirePermission("CollectionPointAccounts.Manage")]
        public async Task<IActionResult> Edit(int id)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Accounts));
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
        [RequirePermission("CollectionPointAccounts.Manage")]
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
                return RedirectToAction(nameof(Accounts));
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
                return RedirectToAction(nameof(Accounts));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تعديل رصيد نقطة تحصيل {AccountId}", id);
                ModelState.AddModelError(string.Empty, $"حدث خطأ أثناء حفظ التعديلات: {ex.Message}");
                return View(model);
            }
        }

        // GET: CollectionPoints/Details/5 - تفاصيل عمليات التحصيل لنقطة تحصيل (مشتركو الشركة فقط)
        [RequirePermission("CollectionPointAccounts.View")]
        public async Task<IActionResult> Details(int id, DateTime? from = null, DateTime? to = null)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (currentUser == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرىجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            // الحساب قد يكون لنقطة مستقلة؛ نعتمد عزل الشركة على معاملات NetworkId للمشتركين.
            CollectionPointAccount? account = await _context.CollectionPointAccounts
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account == null)
            {
                return NotFound();
            }

            bool hasCompanyPayments = await _context.PaymentTransactions
                .AsNoTracking()
                .AnyAsync(t => t.ReceivedByUserId == account.UserId
                               && t.NetworkId == networkId.Value
                               && t.Client != null
                               && t.Client.NetworkId == networkId.Value);
            if (!hasCompanyPayments && account.NetworkId != networkId.Value)
            {
                return NotFound();
            }

            DateTime? fromDt = from?.Date;
            DateTime? toExclusive = to?.Date.AddDays(1);
            if (fromDt.HasValue && to.HasValue && to.Value.Date < fromDt.Value)
            {
                (fromDt, toExclusive) = (to.Value.Date, fromDt.Value.AddDays(1));
                (from, to) = (to.Value.Date, from.Value.Date);
            }

            IQueryable<PaymentTransaction> query = _context.PaymentTransactions
                .AsNoTracking()
                .Include(t => t.Client!)
                    .ThenInclude(c => c.Profile)
                .Where(t => t.ReceivedByUserId == account.UserId
                            && t.NetworkId == networkId.Value
                            && t.Client != null
                            && t.Client.NetworkId == networkId.Value);

            if (fromDt.HasValue)
            {
                query = query.Where(t => t.PaymentDate >= fromDt.Value);
            }

            if (toExclusive.HasValue)
            {
                query = query.Where(t => t.PaymentDate < toExclusive.Value);
            }

            bool hasDateFilter = fromDt.HasValue || toExclusive.HasValue;
            List<PaymentTransaction> transactions = hasDateFilter
                ? await query.OrderByDescending(t => t.PaymentDate).Take(5000).ToListAsync()
                : await query.OrderByDescending(t => t.PaymentDate).Take(500).ToListAsync();

            List<CollectionPointDetailsPrintRow> printRows = new();
            decimal totalWithTax = 0m;
            decimal totalWithoutTax = 0m;
            decimal taxAmount = 0m;
            int sequence = 1;

            foreach (PaymentTransaction t in transactions)
            {
                decimal paid = t.PaymentAmount > 0 ? t.PaymentAmount : t.Amount;
                (decimal baseAmount, decimal rowTax) = SplitTax(t, paid);
                totalWithTax += paid;
                totalWithoutTax += baseAmount;
                taxAmount += rowTax;

                printRows.Add(new CollectionPointDetailsPrintRow
                {
                    Sequence = sequence++,
                    FullName = t.Client?.Name ?? $"عميل رقم {t.ClientId}",
                    PhoneNumber = string.IsNullOrWhiteSpace(t.Client?.PhoneNumber) ? "—" : t.Client!.PhoneNumber!,
                    ProfileSpeed = BuildProfileSpeed(t.Client),
                    OperationTypeLabel = OperationTypeLabel(t.OperationType),
                    AmountPaid = paid,
                    PaymentDate = t.PaymentDate
                });
            }

            CollectionPointDetailsViewModel model = new CollectionPointDetailsViewModel
            {
                AccountId = account.Id,
                UserId = account.UserId,
                UserName = account.User?.FullName
                    ?? account.User?.UserName
                    ?? account.UserId,
                CurrentBalance = account.Balance,
                From = from?.Date,
                To = to?.Date,
                PrintedAt = DateTime.Now,
                TotalWithoutTax = totalWithoutTax,
                TaxAmount = taxAmount,
                TotalWithTax = totalWithTax,
                ResultCount = transactions.Count,
                Transactions = transactions,
                PrintRows = printRows
            };

            ViewBag.CanManage = await _permissionService.HasPermissionAsync(User, "CollectionPointAccounts.Manage");
            return View(model);
        }

        /// <summary>
        /// طلبات تغذية رصيد نقاط التحصيل تُعالَج حصرياً من مدير النظام.
        /// </summary>
        [RequirePermission("CollectionPointAccounts.Manage")]
        public IActionResult TopUpRequests(CollectionPointTopUpStatus? status = null)
        {
            TempData["Info"] = "طلبات تغذية رصيد نقاط التحصيل تُقدَّم وتُراجع حصرياً من قبل مدير النظام.";
            return RedirectToAction(nameof(Accounts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("CollectionPointAccounts.Manage")]
        public IActionResult ApproveTopUpRequest(int id, string? adminNotes = null)
        {
            TempData["Error"] = "لا يمكن معالجة طلبات تغذية نقاط التحصيل من لوحة مدير الشركة؛ يتم ذلك من لوحة مدير النظام فقط.";
            return RedirectToAction(nameof(Accounts));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("CollectionPointAccounts.Manage")]
        public IActionResult RejectTopUpRequest(int id, string? adminNotes = null)
        {
            TempData["Error"] = "لا يمكن معالجة طلبات تغذية نقاط التحصيل من لوحة مدير الشركة؛ يتم ذلك من لوحة مدير النظام فقط.";
            return RedirectToAction(nameof(Accounts));
        }

        private static string OperationTypeLabel(string? operationType) =>
            operationType switch
            {
                "Renewal" => "تجديد",
                "ClientTopUp" => "تغذية رصيد",
                _ => "تحصيل"
            };

        private static (decimal BaseAmount, decimal TaxAmount) SplitTax(PaymentTransaction t, decimal paid)
        {
            decimal vatPct = t.OperationType == "Renewal"
                ? (t.Client?.Profile?.VATPercentage ?? 0m)
                : 0m;
            decimal baseAmount = vatPct > 0m
                ? Math.Round(paid / (1m + (vatPct / 100m)), 2, MidpointRounding.AwayFromZero)
                : paid;
            decimal rowTax = Math.Round(paid - baseAmount, 2, MidpointRounding.AwayFromZero);
            return (baseAmount, rowTax);
        }

        private static string BuildProfileSpeed(Client? client)
        {
            string profileName = client?.Profile?.Name ?? client?.ProfileName ?? "";
            string speed = client?.Profile?.DownloadSpeedDisplay ?? "";
            if (string.IsNullOrWhiteSpace(profileName) && string.IsNullOrWhiteSpace(speed))
            {
                return "—";
            }

            if (string.IsNullOrWhiteSpace(speed))
            {
                return profileName;
            }

            return string.IsNullOrWhiteSpace(profileName) ? speed : $"{profileName} ({speed})";
        }
    }
}
