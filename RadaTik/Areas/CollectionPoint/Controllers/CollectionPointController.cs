using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Domain.CollectionPoint;
using global::RadaTik.Services.CollectionPoint;
using global::RadaTik.ViewModels.CollectionPoint;

namespace RadaTik.Areas.CollectionPoint.Controllers
{
    [Area("CollectionPoint")]
    [Authorize(Roles = RoleNames.CollectionPoint)]
    public class CollectionPointController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<CollectionPointController> _logger;
        private readonly IClientRenewalGuardService _clientRenewalGuardService;
        private readonly ICollectionPaymentService _collectionPaymentService;
        private readonly ICollectionPointReceivePaymentService _receivePaymentService;
        private readonly ICollectionPointRenewalOrchestrator _renewalOrchestrator;
        private readonly ICollectionPointTopUpOrchestrator _topUpOrchestrator;
        private readonly ICurrencyHelper _currencyHelper;
        private readonly ICompanyFinancialHelper _companyFinancial;

        public CollectionPointController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<CollectionPointController> logger,
            IClientRenewalGuardService clientRenewalGuardService,
            ICollectionPaymentService collectionPaymentService,
            ICollectionPointReceivePaymentService receivePaymentService,
            ICollectionPointRenewalOrchestrator renewalOrchestrator,
            ICollectionPointTopUpOrchestrator topUpOrchestrator,
            ICurrencyHelper currencyHelper,
            ICompanyFinancialHelper companyFinancial)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _clientRenewalGuardService = clientRenewalGuardService;
            _collectionPaymentService = collectionPaymentService;
            _receivePaymentService = receivePaymentService;
            _renewalOrchestrator = renewalOrchestrator;
            _topUpOrchestrator = topUpOrchestrator;
            _currencyHelper = currencyHelper;
            _companyFinancial = companyFinancial;
        }

        private IActionResult ApplyOperationOutcome(CollectionPointOperationOutcome outcome)
        {
            if (outcome.NotFound)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(outcome.ErrorMessage))
            {
                TempData["Error"] = outcome.ErrorMessage;
            }

            if (outcome.IsSuccess && !string.IsNullOrEmpty(outcome.SuccessMessage))
            {
                TempData["Success"] = outcome.SuccessMessage;
            }

            return RedirectToAction(outcome.RedirectAction, outcome.RouteValues);
        }

        /// <summary>الحصول على شبكة نقطة التحصيل (من Session أو من حساب نقطة التحصيل)</summary>
        private async Task<int?> GetNetworkIdForCollectionPointAsync(ApplicationUser? user)
        {
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (networkId.HasValue)
            {
                return networkId;
            }

            if (user == null || !User.IsInRole(RoleNames.CollectionPoint))
            {
                return null;
            }

            CollectionPointAccount? acc = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            if (acc?.NetworkId != null)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, acc.NetworkId.Value);
                return acc.NetworkId;
            }
            return null;
        }

        public async Task<IActionResult> Index()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "يرجى تسجيل الدخول.";
                return View(new CollectionPointDashboardViewModel());
            }

            CollectionPointAccount? account = await _context.CollectionPointAccounts
                .Include(a => a.Network)
                .FirstOrDefaultAsync(a => a.UserId == user.Id);

            if (account == null)
            {
                account = new CollectionPointAccount
                {
                    UserId = user.Id,
                    NetworkId = null,
                    Balance = 0m,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                };
                _context.CollectionPointAccounts.Add(account);
                await _context.SaveChangesAsync();
            }

            List<Network> networks = await _context.Networks
                .Where(n => n.Status != NetworkStatus.Inactive)
                .Include(n => n.ManagerUser)
                .Include(n => n.ParentNetwork)
                .OrderBy(n => n.ParentNetworkId.HasValue)
                .ThenBy(n => n.Name)
                .ToListAsync();

            List<NetworkCardItem> networksList = networks.Select(n => new NetworkCardItem
            {
                Id = n.Id,
                Name = n.ParentNetworkId.HasValue && n.ParentNetwork != null
                    ? $"{n.ParentNetwork.Name} — {n.Name}"
                    : n.Name,
                LogoPath = n.LogoPath ?? n.ParentNetwork?.LogoPath,
                Phone = n.ManagerUser?.PhoneNumber ?? n.ManagerUser?.UserName
            }).ToList();

            List<PaymentTransaction> recentTransactions = await _context.PaymentTransactions
                .Where(t => t.ReceivedByUserId == user.Id)
                .Include(t => t.Client)
                .OrderByDescending(t => t.PaymentDate)
                .Take(20)
                .ToListAsync();

            CollectionPointDashboardViewModel model = new CollectionPointDashboardViewModel
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
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new List<ClientSearchResultItem>());
            }

            IQueryable<Client> query = _context.Clients
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

            List<Client> clients = await query.OrderBy(c => c.UserName).Take(50).ToListAsync();
            string networkName = await _context.Networks.Where(n => n.Id == networkId).Select(n => n.Name).FirstOrDefaultAsync() ?? "";
            DateTime now = DateTime.Now;

            CompanyFinancialSnapshot financial = await _companyFinancial.GetSnapshotAsync(networkId);

            List<ClientSearchResultItem> items = new();
            foreach (Client c in clients)
            {
                int pendingMonths = SubscriptionArrearsCalculator.CalculatePendingMonths(c.AccountExpirationDate, now);
                decimal amountPerMonth = c.Profile != null ? c.Profile.Price * (1 + c.Profile.VATPercentage / 100) : 0m;
                decimal amountDueAccount = amountPerMonth * pendingMonths;
                CollectionRenewalQuote quote = _collectionPaymentService.QuoteAccountCharge(
                    c.AccountCurrency,
                    amountDueAccount,
                    financial.DefaultUsdToSypExchangeRate);

                items.Add(new ClientSearchResultItem
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
                    TotalPrice = amountPerMonth,
                    PendingMonths = pendingMonths,
                    AccountCurrency = c.AccountCurrency,
                    ExchangeRate = quote.ExchangeRate,
                    TotalAmountDue = amountDueAccount,
                    TotalAmountDuePointChargeSyp = quote.Success ? quote.PointChargeSyp : amountDueAccount,
                    ProfileDownloadSpeed = c.Profile?.DownloadSpeedMbps ?? 0,
                    ProfileDownloadSpeedDisplay = c.Profile?.DownloadSpeedDisplay ?? ""
                });
            }

            return Json(items);
        }

        /// <summary>تسديد فاتورة المشترك: حسم المبلغ وتجديد الاشتراك مباشرة</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayBill(int clientId)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                TempData["Error"] = "يرجى تسجيل الدخول.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                CollectionPointOperationOutcome outcome = await _renewalOrchestrator.PayBillAsync(
                    new PayBillCommand(clientId, user.Id));
                return ApplyOperationOutcome(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تسديد الفاتورة للمشترك {ClientId}", clientId);
                TempData["Error"] = "حدث خطأ أثناء تسديد الفاتورة.";
                return RedirectToAction(nameof(Index));
            }
        }

        /// <summary>تفاصيل العميل: الشبكة، الباقات وأسعارها، الباقة الحالية، المبلغ الواجب، تسديد</summary>
        [HttpGet]
        public async Task<IActionResult> ClientDetails(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = await GetNetworkIdForCollectionPointAsync(user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "هذه العملية تتطلب اختيار شبكة من لوحة نقطة التحصيل أولاً.";
                return RedirectToAction(nameof(Index));
            }

            Client? client = await _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Network).ThenInclude(n => n!.ParentNetwork)
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client == null)
            {
                return NotFound();
            }

            List<Profile> profiles = await _context.Profiles
                .Where(p => p.NetworkId == networkId.Value && p.IsActive)
                .OrderBy(p => p.DisplayOrder).ThenBy(p => p.Name)
                .ToListAsync();

            CollectionPointAccount? pointAccount = await _context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == user.Id);
            decimal pointBalance = pointAccount?.Balance ?? 0m;
            decimal currentPrice = client.Profile?.Price ?? 0m;
            decimal currentPriceWithVat = client.Profile != null ? client.Profile.Price * (1 + client.Profile.VATPercentage / 100) : 0m;

            List<ClientTopUpTransaction> recentTopUps = await _context.ClientTopUpTransactions
                .Where(t => t.ClientId == client.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(5)
                .Include(t => t.CreatedByUser)
                .ToListAsync();

            CompanyFinancialSnapshot financial = await _companyFinancial.GetSnapshotAsync(networkId.Value);
            int pendingMonths = SubscriptionArrearsCalculator.CalculatePendingMonths(client.AccountExpirationDate, DateTime.Now);
            decimal amountDueAccount = currentPriceWithVat * pendingMonths;
            CollectionRenewalQuote renewalQuote = _collectionPaymentService.QuoteAccountCharge(
                client.AccountCurrency,
                amountDueAccount,
                financial.DefaultUsdToSypExchangeRate);

            ClientDetailsForCollectionPointViewModel model = new ClientDetailsForCollectionPointViewModel
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
                AccountCurrency = client.AccountCurrency,
                RequiresExchange = _currencyHelper.RequiresExchangeAtCollection(client.AccountCurrency),
                DefaultExchangeRate = financial.DefaultUsdToSypExchangeRate,
                PendingRenewalMonths = pendingMonths,
                AmountDueAccount = amountDueAccount,
                AmountDuePointChargeSyp = renewalQuote.Success ? renewalQuote.PointChargeSyp : amountDueAccount,
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
                AmountDue = amountDueAccount,
                CollectionPointBalance = pointBalance,
                AccountExpirationDate = client.AccountExpirationDate
            };

            RenewalBlockResult renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
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
        public async Task<IActionResult> TopUpClientBalance(int clientId, decimal amount, decimal? exchangeRate, string? notes)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = await GetNetworkIdForCollectionPointAsync(user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "هذه العملية تتطلب اختيار شبكة من لوحة نقطة التحصيل أولاً.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }

            try
            {
                CollectionPointOperationOutcome outcome = await _topUpOrchestrator.TopUpAsync(
                    new TopUpClientBalanceCommand(
                        clientId,
                        networkId.Value,
                        user.Id,
                        user.FullName ?? user.UserName,
                        amount,
                        exchangeRate,
                        notes));
                return ApplyOperationOutcome(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تغذية رصيد العميل {ClientId}", clientId);
                TempData["Error"] = "حدث خطأ أثناء تغذية الرصيد.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }
        }

        /// <summary>تسديد وتجديد اشتراك مباشر من صفحة التفاصيل</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayAndRequestRenewal(int clientId, decimal amount, string? notes)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = await GetNetworkIdForCollectionPointAsync(user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "هذه العملية تتطلب اختيار شبكة من لوحة نقطة التحصيل أولاً.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                CollectionPointOperationOutcome outcome = await _renewalOrchestrator.PayAndRenewAsync(
                    new PayAndRenewCommand(clientId, networkId.Value, user.Id, notes));
                return ApplyOperationOutcome(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تسديد وتقديم طلب تجديد للعميل {ClientId}", clientId);
                TempData["Error"] = "حدث خطأ أثناء تنفيذ العملية.";
                return RedirectToAction(nameof(ClientDetails), new { id = clientId });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ReceivePayment(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = await GetNetworkIdForCollectionPointAsync(user);

            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "هذه العملية تتطلب اختيار شبكة من لوحة نقطة التحصيل أولاً.";
                return RedirectToAction(nameof(Index));
            }

            Client? client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client == null)
            {
                return NotFound();
            }

            CompanyFinancialSnapshot financial = await _companyFinancial.GetSnapshotAsync(
                client.NetworkId ?? networkId.Value);

            bool requiresExchange = CurrencyHelper.RequiresExchangeAtCollection(client.AccountCurrency);
            return View(new ReceivePaymentViewModel
            {
                ClientId = client.Id,
                ClientName = client.Name,
                ClientUserName = client.UserName,
                CurrentClientBalance = client.Balance,
                AccountCurrency = client.AccountCurrency,
                RequiresExchange = requiresExchange,
                ExchangeRate = financial.DefaultUsdToSypExchangeRate
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceivePayment(ReceivePaymentViewModel model)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = await GetNetworkIdForCollectionPointAsync(user);

            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "هذه العملية تتطلب اختيار شبكة من لوحة نقطة التحصيل أولاً.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            try
            {
                ReceivePaymentOutcome outcome = await _receivePaymentService.ProcessAsync(
                    new ReceivePaymentCommand(
                        model.ClientId,
                        model.Amount,
                        model.ExchangeRate,
                        model.Notes,
                        user.Id,
                        networkId.Value));

                if (outcome.NotFound)
                {
                    return NotFound();
                }

                if (outcome.ReturnView && outcome.ViewModel != null)
                {
                    if (!string.IsNullOrEmpty(outcome.ErrorMessage))
                    {
                        ModelState.AddModelError(string.Empty, outcome.ErrorMessage);
                    }

                    return View(outcome.ViewModel);
                }

                if (!outcome.IsSuccess)
                {
                    ModelState.AddModelError(string.Empty, outcome.ErrorMessage ?? "تعذر تسجيل التحصيل.");
                    return View(model);
                }

                TempData["Success"] = outcome.SuccessMessage;
                return RedirectToAction(nameof(Index), new { q = outcome.RedirectSearchQuery });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ أثناء تسجيل التحصيل للعميل {ClientId}", model.ClientId);
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تسجيل التحصيل.");
                return View(model);
            }
        }

        public async Task<IActionResult> History()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            List<PaymentTransaction> txs = await _context.PaymentTransactions
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

