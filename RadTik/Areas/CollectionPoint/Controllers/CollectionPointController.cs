using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.ViewModels.CollectionPoint;

namespace RadTik.Areas.CollectionPoint.Controllers
{
    [Area("CollectionPoint")]
    [Authorize(Roles = RoleNames.CollectionPoint)]
    public class CollectionPointController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CollectionPointController> _logger;
        private readonly RequestNotificationService _requestNotificationService;
        private readonly ICollectionCommissionChargeService _collectionCommissionChargeService;
        private readonly IClientRenewalGuardService _clientRenewalGuardService;

        public CollectionPointController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CollectionPointController> logger,
            RequestNotificationService requestNotificationService,
            ICollectionCommissionChargeService collectionCommissionChargeService,
            IClientRenewalGuardService clientRenewalGuardService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _requestNotificationService = requestNotificationService;
            _collectionCommissionChargeService = collectionCommissionChargeService;
            _clientRenewalGuardService = clientRenewalGuardService;
        }

        private static int CalculatePendingMonths(DateTime? accountExpirationDate, DateTime now)
        {
            if (!accountExpirationDate.HasValue || accountExpirationDate.Value >= now)
            {
                return 1;
            }

            var expiredDate = accountExpirationDate.Value.Date;
            var today = now.Date;
            var months = (today.Year - expiredDate.Year) * 12 + today.Month - expiredDate.Month;
            if (today.Day > expiredDate.Day)
            {
                months++;
            }

            return Math.Max(1, months);
        }

        private static string BuildReferenceNumber(string operationType)
        {
            return $"{operationType}-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}"[..40];
        }

        /// <summary>الحصول على شبكة نقطة التحصيل (من Session أو من حساب نقطة التحصيل)</summary>
        private async Task<int?> GetNetworkIdForCollectionPointAsync(ApplicationUser? user)
        {
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (networkId.HasValue) return networkId;
            if (user == null || !User.IsInRole(RoleNames.CollectionPoint)) return null;
            var acc = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (acc?.NetworkId != null)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, acc.NetworkId.Value);
                return acc.NetworkId;
            }
            return null;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "يرجى تسجيل الدخول.";
                return View(new CollectionPointDashboardViewModel());
            }

            var account = await _context.CollectionPointAccounts
                .Include(a => a.Network)
                .FirstOrDefaultAsync(a => a.UserId == user.Id);

            if (account == null)
            {
                var firstNetwork = await _context.Networks.Where(n => n.Status == NetworkStatus.Active).FirstOrDefaultAsync();
                if (firstNetwork != null)
                {
                    account = new CollectionPointAccount
                    {
                        UserId = user.Id,
                        NetworkId = firstNetwork.Id,
                        Balance = 0m,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.CollectionPointAccounts.Add(account);
                    await _context.SaveChangesAsync();
                }
            }

            var networks = await _context.Networks
                .Where(n => n.Status == NetworkStatus.Active)
                .Include(n => n.ManagerUser)
                .OrderBy(n => n.Name)
                .ToListAsync();

            var networksList = networks.Select(n => new NetworkCardItem
            {
                Id = n.Id,
                Name = n.Name,
                LogoPath = n.LogoPath,
                Phone = n.ManagerUser?.PhoneNumber ?? n.ManagerUser?.UserName
            }).ToList();

            var recentTransactions = await _context.PaymentTransactions
                .Where(t => t.ReceivedByUserId == user.Id)
                .Include(t => t.Client)
                .OrderByDescending(t => t.PaymentDate)
                .Take(20)
                .ToListAsync();

            var model = new CollectionPointDashboardViewModel
            {
                AccountBalance = account?.Balance ?? 0m,
                Networks = networksList,
                RecentTransactions = recentTransactions
            };

            return View(model);
        }

        /// <summary>بحث المشتركين ضمن شبكة (للـ AJAX)</summary>
        [HttpGet]
        public async Task<IActionResult> SearchClients(int networkId, string? q = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Json(new List<ClientSearchResultItem>());

            var query = _context.Clients
                .Where(c => c.NetworkId == networkId)
                .Include(c => c.Profile)
                .Include(c => c.Network)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(q))
            {
                q = q.Trim();
                query = query.Where(c =>
                    (c.Name != null && c.Name.Contains(q)) ||
                    (c.UserName != null && c.UserName.Contains(q)) ||
                    (c.SID != null && c.SID.Contains(q)) ||
                    (c.PhoneNumber != null && c.PhoneNumber.Contains(q)));
            }

            var clients = await query.OrderBy(c => c.UserName).Take(50).ToListAsync();
            var networkName = await _context.Networks.Where(n => n.Id == networkId).Select(n => n.Name).FirstOrDefaultAsync() ?? "";
            var now = DateTime.Now;

            var items = clients.Select(c => new ClientSearchResultItem
            {
                Id = c.Id,
                UserName = c.UserName ?? "",
                FullName = c.Name ?? "",
                SubscriberNumber = c.SID ?? "",
                NetworkName = networkName,
                PhoneNumber = c.PhoneNumber,
                ProfileName = c.Profile?.Name ?? c.ProfileName ?? "",
                BasePrice = c.Profile?.Price ?? 0m,
                CommissionAmount = c.Profile != null ? c.Profile.Price * (c.Profile.VATPercentage / 100) : 0m,
                TotalPrice = c.Profile != null ? c.Profile.Price * (1 + c.Profile.VATPercentage / 100) : 0m,
                PendingMonths = CalculatePendingMonths(c.AccountExpirationDate, now),
                TotalAmountDue = (c.Profile != null ? c.Profile.Price * (1 + c.Profile.VATPercentage / 100) : 0m) * CalculatePendingMonths(c.AccountExpirationDate, now),
                ProfileDownloadSpeed = c.Profile?.DownloadSpeedMbps ?? 0,
                ProfileDownloadSpeedDisplay = c.Profile?.DownloadSpeedDisplay ?? ""
            }).ToList();

            return Json(items);
        }

        /// <summary>تسديد فاتورة المشترك: حسم المبلغ وتجديد الاشتراك مباشرة</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayBill(int clientId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "يرجى تسجيل الدخول.";
                return RedirectToAction(nameof(Index));
            }

            var client = await _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Network)
                .FirstOrDefaultAsync(c => c.Id == clientId);
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على المشترك.";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (account == null)
            {
                TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل.";
                return RedirectToAction(nameof(Index));
            }

            var basePricePerMonth = client.Profile?.Price ?? 0m;
            var vatPercentage = client.Profile?.VATPercentage ?? 0m;
            var vatPerMonth = basePricePerMonth * (vatPercentage / 100m);
            var amountPerMonth = basePricePerMonth + vatPerMonth;
            if (amountPerMonth <= 0)
            {
                TempData["Error"] = "لا يوجد سعر محدد للباقة.";
                return RedirectToAction(nameof(Index));
            }
            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                TempData["Error"] =
                    $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة على المشترك (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
                return RedirectToAction(nameof(Index));
            }

            var pendingMonths = CalculatePendingMonths(client.AccountExpirationDate, DateTime.Now);
            var amountDue = amountPerMonth * pendingMonths;
            if (account.Balance < amountDue)
            {
                var totalBase = basePricePerMonth * pendingMonths;
                var totalVat = vatPerMonth * pendingMonths;
                TempData["Error"] =
                    $"رصيد نقطة التحصيل غير كافٍ. المطلوب: {amountDue:N0} ل.س (الأساسي: {totalBase:N0} + الضريبة {vatPercentage:N2}%: {totalVat:N0}) والرصيد: {account.Balance:N0} ل.س";
                return RedirectToAction(nameof(Index));
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var prevPointBalance = account.Balance;
                account.Balance -= amountDue;
                account.UpdatedAt = DateTime.Now;

                var referenceNumber = BuildReferenceNumber("REN");
                var payment = new PaymentTransaction
                {
                    ClientId = client.Id,
                    NetworkId = client.NetworkId,
                    Amount = amountDue,
                    PaymentDate = DateTime.Now,
                    ReceivedByUserId = user.Id,
                    OperationType = "Renewal",
                    ReferenceNumber = referenceNumber,
                    Notes = pendingMonths > 1
                        ? $"تسديد متأخر {pendingMonths} أشهر وتجديد مباشر"
                        : "تسديد فاتورة وتجديد مباشر",
                    PreviousClientBalance = client.Balance,
                    NewClientBalance = client.Balance,
                    PreviousPointBalance = prevPointBalance,
                    NewPointBalance = account.Balance
                };
                _context.PaymentTransactions.Add(payment);

                var baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                    ? client.AccountExpirationDate.Value
                    : client.AccountExpirationDate ?? DateTime.Now;
                client.AccountExpirationDate = baseDate.AddMonths(pendingMonths);
                client.LastRenewalDate = DateTime.Now;
                client.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                var commission = await _collectionCommissionChargeService.ChargeAfterPaymentRecordedAsync(payment.Id, amountDue);
                if (!commission.Success)
                {
                    await tx.RollbackAsync();
                    TempData["Error"] = commission.ErrorMessage ?? "تعذر إتمام عمولة التحصيل (محفظة الشركة).";
                    return RedirectToAction(nameof(Index));
                }

                await tx.CommitAsync();

                var successBase = basePricePerMonth * pendingMonths;
                var successVat = vatPerMonth * pendingMonths;
                TempData["Success"] =
                    $"تم تسديد {pendingMonths} شهر/أشهر بمبلغ {amountDue:N0} ل.س (الأساسي: {successBase:N0} + الضريبة {vatPercentage:N2}%: {successVat:N0}) وتجديد اشتراك {client.UserName} مباشرة حتى {client.AccountExpirationDate:yyyy/MM/dd}. المرجع: {referenceNumber}";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطأ في تسديد الفاتورة للمشترك {ClientId}", clientId);
                TempData["Error"] = "حدث خطأ أثناء تسديد الفاتورة.";
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>تفاصيل العميل: الشبكة، الباقات وأسعارها، الباقة الحالية، المبلغ الواجب، تسديد</summary>
        [HttpGet]
        public async Task<IActionResult> ClientDetails(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = await GetNetworkIdForCollectionPointAsync(user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
                return RedirectToAction(nameof(Index));
            }

            var client = await _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Network).ThenInclude(n => n!.ParentNetwork)
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client == null) return NotFound();

            var profiles = await _context.Profiles
                .Where(p => p.NetworkId == networkId.Value && p.IsActive)
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                .ToListAsync();

            var pointAccount = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            var pointBalance = pointAccount?.Balance ?? 0m;
            var currentPrice = client.Profile?.Price ?? 0m;
            var currentPriceWithVat = client.Profile != null ? client.Profile.Price * (1 + client.Profile.VATPercentage / 100) : 0m;

            var recentTopUps = await _context.ClientTopUpTransactions
                .Where(t => t.ClientId == client.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Include(t => t.CreatedByUser)
                .ToListAsync();

            var model = new ClientDetailsForCollectionPointViewModel
            {
                ClientId = client.Id,
                ClientName = client.Name,
                ClientUserName = client.UserName,
                PhoneNumber = client.PhoneNumber,
                NetworkId = networkId.Value,
                NetworkName = client.Network?.Name,
                CompanyName = client.Network?.ParentNetwork?.Name ?? client.Network?.Name,
                CurrentProfileId = client.ProfileId,
                CurrentProfileName = client.Profile?.Name,
                CurrentProfilePrice = currentPriceWithVat,
                CurrentBasePrice = currentPrice,
                CurrentCommissionAmount = currentPriceWithVat - currentPrice,
                ClientBalance = client.Balance,
                ResidenceAddress = client.ResidenceAddress,
                ProfilePrices = profiles.Select(p => new ProfilePriceItem
                {
                    ProfileId = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    SpeedDisplay = p.DownloadSpeedDisplay,
                    DataLimitDisplay = p.DataLimit.HasValue && p.DataLimit.Value > 0 ? $"{p.DataLimit:0.##} GB" : "غير محدد",
                    CommissionAmount = p.Price * (p.VATPercentage / 100),
                    PriceWithVAT = p.Price * (1 + p.VATPercentage / 100)
                }).ToList(),
                AmountDue = currentPriceWithVat,
                CollectionPointBalance = pointBalance,
                AccountExpirationDate = client.AccountExpirationDate
            };

            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            ViewBag.HasBlockingInvoices = !renewalGuard.CanRenew;
            ViewBag.BlockingInvoicesCount = renewalGuard.PendingInvoicesCount;
            ViewBag.BlockingInvoicesTotal = renewalGuard.TotalOutstanding;

            ViewBag.ClientBalance = client.Balance;
            ViewBag.RecentTopUps = recentTopUps;

            return View(model);
        }

        /// <summary>تغذية رصيد العميل - من نقطة التحصيل (خصم من رصيد نقطة التحصيل)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TopUpClientBalance(int clientId, decimal amount, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = await GetNetworkIdForCollectionPointAsync(user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }
            if (amount < 0.01m)
            {
                TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId.Value);
            if (client == null) return NotFound();

            var account = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (account == null || account.Balance < amount)
            {
                TempData["Error"] = "رصيد نقطة التحصيل غير كافٍ لإتمام التغذية المطلوبة.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var prevBalance = client.Balance;
                client.Balance += amount;
                client.LastUpdated = DateTime.Now;

                account.Balance -= amount;
                account.UpdatedAt = DateTime.Now;

                var referenceNumber = BuildReferenceNumber("TOP");
                _context.ClientTopUpTransactions.Add(new ClientTopUpTransaction
                {
                    ClientId = client.Id,
                    Amount = amount,
                    PreviousBalance = prevBalance,
                    NewBalance = client.Balance,
                    SourceType = ClientTopUpSource.CollectionPoint,
                    CreatedByUserId = user.Id,
                    CollectionPointAccountId = account.Id,
                    Notes = notes?.Trim()
                });

                _context.PaymentTransactions.Add(new PaymentTransaction
                {
                    ClientId = client.Id,
                    NetworkId = networkId.Value,
                    Amount = amount,
                    PaymentDate = DateTime.Now,
                    ReceivedByUserId = user.Id,
                    OperationType = "ClientTopUp",
                    ReferenceNumber = referenceNumber,
                    Notes = string.IsNullOrWhiteSpace(notes) ? "تغذية رصيد مشترك من نقطة التحصيل" : notes.Trim(),
                    PreviousClientBalance = prevBalance,
                    NewClientBalance = client.Balance,
                    PreviousPointBalance = account.Balance + amount,
                    NewPointBalance = account.Balance
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _requestNotificationService.NotifyClientTopUpSubmittedAsync(
                    client.Id,
                    networkId,
                    amount,
                    "نقطة التحصيل",
                    user.FullName ?? user.UserName);

                TempData["Success"] = $"تم تغذية رصيد العميل بمبلغ {amount:N0} ل.س. المرجع: {referenceNumber}. الرصيد الحالي: {client.Balance:N0} ل.س";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطأ في تغذية رصيد العميل {ClientId}", clientId);
                TempData["Error"] = "حدث خطأ أثناء تغذية الرصيد.";
            }

            return RedirectToAction(nameof(ClientDetails), new { id = clientId });
        }

        /// <summary>تسديد وتجديد اشتراك مباشر من صفحة التفاصيل</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayAndRequestRenewal(int clientId, decimal amount, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = await GetNetworkIdForCollectionPointAsync(user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
                return RedirectToAction(nameof(Index));
            }
            var client = await _context.Clients.Include(c => c.Profile).FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId.Value);
            if (client == null) return NotFound();

            var basePricePerMonth = client.Profile?.Price ?? 0m;
            var vatPercentage = client.Profile?.VATPercentage ?? 0m;
            var vatPerMonth = basePricePerMonth * (vatPercentage / 100m);
            var amountPerMonth = basePricePerMonth + vatPerMonth;
            if (amountPerMonth <= 0m)
            {
                TempData["Error"] = "لا يوجد سعر صالح لباقة المشترك.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }
            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                TempData["Error"] =
                    $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة على المشترك (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }

            var pendingMonths = CalculatePendingMonths(client.AccountExpirationDate, DateTime.Now);
            var amountDue = amountPerMonth * pendingMonths;

            var account = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (account == null || account.Balance < amountDue)
            {
                var totalBase = basePricePerMonth * pendingMonths;
                var totalVat = vatPerMonth * pendingMonths;
                TempData["Error"] =
                    $"رصيد نقطة التحصيل غير كافٍ. المطلوب {amountDue:N0} ل.س (الأساسي: {totalBase:N0} + الضريبة {vatPercentage:N2}%: {totalVat:N0}).";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var prevPointBalance = account.Balance;
                account.Balance -= amountDue;
                account.UpdatedAt = DateTime.Now;

                var referenceNumber = BuildReferenceNumber("REN");
                var payment = new PaymentTransaction
                {
                    ClientId = client.Id,
                    NetworkId = networkId.Value,
                    Amount = amountDue,
                    PaymentDate = DateTime.Now,
                    ReceivedByUserId = user.Id,
                    OperationType = "Renewal",
                    ReferenceNumber = referenceNumber,
                    Notes = notes ?? (pendingMonths > 1 ? $"تجديد مباشر عن {pendingMonths} أشهر متأخرة" : "تجديد مباشر"),
                    PreviousClientBalance = client.Balance,
                    NewClientBalance = client.Balance,
                    PreviousPointBalance = prevPointBalance,
                    NewPointBalance = account.Balance
                };
                _context.PaymentTransactions.Add(payment);

                var baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                    ? client.AccountExpirationDate.Value
                    : client.AccountExpirationDate ?? DateTime.Now;
                client.AccountExpirationDate = baseDate.AddMonths(pendingMonths);
                client.LastRenewalDate = DateTime.Now;
                client.LastUpdated = DateTime.Now;

                await _context.SaveChangesAsync();

                var commission = await _collectionCommissionChargeService.ChargeAfterPaymentRecordedAsync(payment.Id, amountDue);
                if (!commission.Success)
                {
                    await tx.RollbackAsync();
                    TempData["Error"] = commission.ErrorMessage ?? "تعذر إتمام عمولة التحصيل (محفظة الشركة).";
                    return RedirectToAction(nameof(ClientDetails), new { id = clientId });
                }

                await tx.CommitAsync();

                var successBase = basePricePerMonth * pendingMonths;
                var successVat = vatPerMonth * pendingMonths;
                TempData["Success"] =
                    $"تم التجديد المباشر بنجاح ({pendingMonths} شهر/أشهر) بمبلغ {amountDue:N0} ل.س (الأساسي: {successBase:N0} + الضريبة {vatPercentage:N2}%: {successVat:N0}). المرجع: {referenceNumber}";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطأ في تسديد وتقديم طلب تجديد للعميل {ClientId}", clientId);
                TempData["Error"] = "حدث خطأ أثناء تنفيذ العملية.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ReceivePayment(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = await GetNetworkIdForCollectionPointAsync(user);

            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
                return RedirectToAction(nameof(Index));
            }

            var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client == null)
            {
                return NotFound();
            }

            return View(new ReceivePaymentViewModel
            {
                ClientId = client.Id,
                ClientName = client.Name,
                ClientUserName = client.UserName,
                CurrentClientBalance = client.Balance
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceivePayment(ReceivePaymentViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = await GetNetworkIdForCollectionPointAsync(user);

            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حساب نقطة التحصيل بأي شبكة.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var client = await _context.Clients
                    .FirstOrDefaultAsync(c => c.Id == model.ClientId && c.NetworkId == networkId.Value);
                if (client == null)
                {
                    return NotFound();
                }

                var account = await _context.CollectionPointAccounts
                    .FirstOrDefaultAsync(a => a.UserId == user.Id);
                if (account == null)
                {
                    account = new CollectionPointAccount
                    {
                        UserId = user.Id,
                        NetworkId = networkId.Value,
                        Balance = 0m,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };
                    _context.CollectionPointAccounts.Add(account);
                    await _context.SaveChangesAsync();
                }

                var previousClientBalance = client.Balance;
                var previousPointBalance = account.Balance;

                client.Balance = client.Balance + model.Amount;
                client.LastUpdated = DateTime.Now;

                account.Balance = account.Balance + model.Amount;
                account.UpdatedAt = DateTime.Now;

                var payment = new PaymentTransaction
                {
                    ClientId = client.Id,
                    NetworkId = networkId.Value,
                    Amount = model.Amount,
                    PaymentDate = DateTime.Now,
                    ReceivedByUserId = user.Id,
                    OperationType = "ReceivePayment",
                    ReferenceNumber = BuildReferenceNumber("REC"),
                    Notes = model.Notes,
                    PreviousClientBalance = previousClientBalance,
                    NewClientBalance = client.Balance,
                    PreviousPointBalance = previousPointBalance,
                    NewPointBalance = account.Balance
                };

                _context.PaymentTransactions.Add(payment);
                _context.Update(client);
                _context.Update(account);
                await _context.SaveChangesAsync();

                var commission = await _collectionCommissionChargeService.ChargeAfterPaymentRecordedAsync(payment.Id, model.Amount);
                if (!commission.Success)
                {
                    await tx.RollbackAsync();
                    ModelState.AddModelError(string.Empty, commission.ErrorMessage ?? "تعذر إتمام عمولة التحصيل (محفظة الشركة).");
                    return View(model);
                }

                await tx.CommitAsync();

                TempData["Success"] = $"تم تسجيل التحصيل بنجاح (+{model.Amount:0.##}) للعميل {client.Name}";
                return RedirectToAction(nameof(Index), new { q = model.ClientUserName });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطأ أثناء تسجيل التحصيل للعميل {ClientId}", model.ClientId);
                ModelState.AddModelError(string.Empty, $"حدث خطأ أثناء تسجيل التحصيل: {ex.Message}");
                return View(model);
            }
        }

        public async Task<IActionResult> History()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var txs = await _context.PaymentTransactions
                .Where(t => t.ReceivedByUserId == user.Id)
                .Include(t => t.Client)
                .Include(t => t.ReceivedByUser)
                .OrderByDescending(t => t.PaymentDate)
                .Take(200)
                .ToListAsync();

            return View(txs);
        }
    }
}

