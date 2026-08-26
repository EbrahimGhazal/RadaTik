using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using RadaTik.ViewModels.ClientPortal;

namespace RadaTik.Controllers
{
    /// <summary>
    /// بوابة العميل - صفحات خاصة بالعملاء (المشتركين)
    /// </summary>
    [Authorize(Roles = "Client")]
    public class ClientPortalController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ClientPortalController> _logger;
        private readonly IMikroTikPppoeUserService? _mikroTikService;
        private readonly IRequestNotificationService _requestNotificationService;
        private readonly IMaintenanceBillingService _maintenanceBillingService;
        private readonly IClientRenewalGuardService _clientRenewalGuardService;
        private readonly ICollectionCommissionChargeService _collectionCommissionChargeService;
        private readonly IClientPortalSelfRenewOrchestrator _clientPortalSelfRenew;
        private readonly IWebHostEnvironment _environment;
        private readonly IMaintenanceEmployeeTaskService _maintenanceEmployeeTasks;
        private readonly IClientVipPolicyService _vipPolicy;

        public ClientPortalController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ClientPortalController> logger,
            IRequestNotificationService requestNotificationService,
            IMaintenanceBillingService maintenanceBillingService,
            IClientRenewalGuardService clientRenewalGuardService,
            ICollectionCommissionChargeService collectionCommissionChargeService,
            IClientPortalSelfRenewOrchestrator clientPortalSelfRenew,
            IWebHostEnvironment environment,
            IMaintenanceEmployeeTaskService maintenanceEmployeeTasks,
            IClientVipPolicyService vipPolicy,
            IMikroTikPppoeUserService? mikroTikService = null)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _requestNotificationService = requestNotificationService;
            _maintenanceBillingService = maintenanceBillingService;
            _clientRenewalGuardService = clientRenewalGuardService;
            _collectionCommissionChargeService = collectionCommissionChargeService;
            _clientPortalSelfRenew = clientPortalSelfRenew;
            _environment = environment;
            _maintenanceEmployeeTasks = maintenanceEmployeeTasks;
            _vipPolicy = vipPolicy;
            _mikroTikService = mikroTikService;
        }

        /// <summary>
        /// الحصول على العميل الحالي المرتبط بالمستخدم
        /// </summary>
        private async Task<Client?> GetCurrentClientAsync()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user?.ClientId == null)
            {
                return null;
            }

            return await _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Receiver)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == user.ClientId);
        }

        private async Task PopulateAssignableEmployeesAsync(int clientId, string? selectedUserId)
        {
            int? companyNetworkId = await _maintenanceEmployeeTasks.ResolveCompanyNetworkIdForClientAsync(clientId);
            if (!companyNetworkId.HasValue)
            {
                ViewBag.AssignableEmployees = new List<SelectListItem>();
                return;
            }

            ViewBag.AssignableEmployees = await _maintenanceEmployeeTasks.GetAssignableEmployeeSelectItemsAsync(
                companyNetworkId.Value,
                selectedUserId);
        }

        #region لوحة التحكم الرئيسية

        /// <summary>
        /// الصفحة الرئيسية للعميل
        /// </summary>
        public async Task<IActionResult> Index()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك. قد يكون حسابك غير مرتبط بعقد اشتراك. يرجى التواصل مع الإدارة.";
                return RedirectToRoute("clientPortal-actions", new { action = "MyProfile" });
            }

            // جلب إحصائيات الطلبات
            ViewBag.PendingMaintenanceRequests = await _context.MaintenanceRequests
                .CountAsync(m => m.ClientId == client.Id && m.Status == MaintenanceRequestStatus.Pending);

            ViewBag.PendingSpeedChangeRequests = await _context.SpeedChangeRequests
                .CountAsync(s => s.ClientId == client.Id && s.Status == SpeedChangeRequestStatus.Pending);

            ViewBag.HasLiveTraffic = false;
            if (client.MikroTikServerId.HasValue && client.MikroTikServer != null)
            {
                MikroTikServer srv = client.MikroTikServer;
                if (srv.IsActive && srv.NetworkId.HasValue)
                {
                    ViewBag.HasLiveTraffic = true;
                    ViewBag.TrafficNetworkId = srv.NetworkId.Value;
                    ViewBag.TrafficServerId = srv.Id;
                    ViewBag.TrafficPppUser = client.UserName ?? "";
                }
            }

            return View(client);
        }

        [HttpGet]
        public async Task<IActionResult> UnreadNotificationsCount()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { count = 0 });
            }

            int count = await _context.UserNotifications
                .AsNoTracking()
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications(bool unreadOnly = false)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            IQueryable<UserNotification> query = _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == user.Id);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            List<UserNotification> items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.UnreadOnly = unreadOnly;
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> OpenNotification(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            UserNotification? row = await _context.UserNotifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == user.Id);
            if (row == null)
            {
                TempData["Error"] = "لم يتم العثور على الإشعار المطلوب.";
                return RedirectToAction(nameof(Notifications));
            }

            if (!row.IsRead)
            {
                row.IsRead = true;
                row.ReadAt = DateTime.Now;
                await _context.SaveChangesAsync();
            }

            string? targetUrl = await ResolveClientNotificationTargetUrlAsync(row);
            if (!string.IsNullOrWhiteSpace(targetUrl) && Url.IsLocalUrl(targetUrl))
            {
                return Redirect(targetUrl);
            }

            return RedirectToAction(nameof(Notifications));
        }

        private async Task<string?> ResolveClientNotificationTargetUrlAsync(UserNotification notification)
        {
            switch (notification.Type)
            {
                case NotificationType.SubscriptionExpiring:
                    return Url.RouteUrl("clientPortal-actions", new { action = nameof(RenewSubscription) });
                case NotificationType.MaintenanceRequestSubmitted:
                    return Url.RouteUrl("clientPortal-actions", new { action = nameof(MaintenanceRequests) });
                case NotificationType.SpeedChangeRequestSubmitted:
                    return Url.RouteUrl("clientPortal-actions", new { action = nameof(SpeedChangeRequests) });
                case NotificationType.MaintenanceInvoiceIssued:
                    {
                        int? invoiceId = TryParseNotificationEntityId(notification.Key);
                        if (invoiceId.HasValue)
                        {
                            bool exists = await _context.MaintenanceInvoices
                                .AsNoTracking()
                                .AnyAsync(i => i.Id == invoiceId.Value);
                            if (exists)
                            {
                                return Url.RouteUrl("clientPortal-actions", new { action = nameof(MaintenanceInvoices), id = invoiceId.Value });
                            }
                        }

                        return Url.RouteUrl("clientPortal-actions", new { action = nameof(MaintenanceInvoices) });
                    }
                default:
                    return Url.RouteUrl("clientPortal-actions", new { action = nameof(Notifications) });
            }
        }

        private static int? TryParseNotificationEntityId(string? key)
        {
            string[] parts = (key ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            return int.TryParse(parts[1], out int id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> MyTraffic()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            if (!client.IsActive || !client.MikroTikServerId.HasValue || string.IsNullOrWhiteSpace(client.UserName))
            {
                TempData["Error"] = "تعذر تحديد اتصال MikroTik الخاص بحسابك.";
                return RedirectToAction(nameof(Index));
            }

            string? serverName = client.MikroTikServer?.Name;
            if (string.IsNullOrWhiteSpace(serverName))
            {
                serverName = await _context.MikroTikServers.AsNoTracking()
                    .Where(s => s.Id == client.MikroTikServerId.Value)
                    .Select(s => s.Name)
                    .FirstOrDefaultAsync();
            }

            ViewBag.ClientName = client.Name ?? client.UserName;
            ViewBag.ClientUserName = client.UserName;
            ViewBag.ServerName = string.IsNullOrWhiteSpace(serverName) ? "—" : serverName;
            return View();
        }

        /// <summary>تجديد الاشتراك من المحفظة (المشترك يدخل ويضغط تجديد عند كفاية الرصيد)</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelfRenewSubscription()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            ApplicationUser? appUser = await _userManager.GetUserAsync(User);
            ClientPortalSelfRenewOutcome outcome = await _clientPortalSelfRenew.ExecuteAsync(new ClientPortalSelfRenewCommand
            {
                ClientId = client.Id,
                ActorUserId = appUser?.Id ?? string.Empty
            });

            if (outcome.Status == ClientPortalSelfRenewStatus.Success)
            {
                TempData["Success"] = outcome.Message;
            }
            else
            {
                if (outcome.Status == ClientPortalSelfRenewStatus.Error)
                {
                    _logger.LogWarning("Portal self-renew failed for client {ClientId}: {Message}", client.Id, outcome.Message);
                }

                TempData["Error"] = outcome.Message;
            }

            if (outcome.RedirectToMaintenanceInvoices)
            {
                return RedirectToAction(nameof(MaintenanceInvoices));
            }

            if (outcome.RedirectToRenewSubscription)
            {
                return RedirectToAction(nameof(RenewSubscription));
            }

            return RedirectToAction(nameof(Index));
        }

        /// <summary>صفحة طلب تغذية رصيد المحفظة</summary>
        public async Task<IActionResult> RequestTopUp()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            int? networkId = await ResolveClientNetworkIdAsync(client.Id);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حسابك بشبكة. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            ClientPortalTopUpRequestViewModel model = await BuildTopUpRequestViewModelAsync(client, networkId.Value);
            if (model.PaymentMethodOptions.Count == 0)
            {
                TempData["Error"] = "لا توجد طرق دفع مفعّلة في النظام. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        /// <summary>تقديم طلب تغذية رصيد من البوابة</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTopUp(ClientPortalTopUpRequestViewModel model)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            int? networkId = await ResolveClientNetworkIdAsync(client.Id);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حسابك بشبكة. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            List<PaymentMethod> activeMethods = await _context.PaymentMethods
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Name)
                .ToListAsync();
            List<ClientPortalPaymentMethodOption> paymentOptions = BuildClientPortalPaymentMethodOptions(activeMethods);
            HashSet<int> allowedIds = paymentOptions.Select(p => p.Id).ToHashSet();
            if (!allowedIds.Contains(model.PaymentMethodId))
            {
                ModelState.AddModelError(nameof(model.PaymentMethodId), "طريقة الدفع غير صالحة.");
            }

            if (model.RecipientTarget == ClientWalletTopUpRecipientTarget.CollectionPoint)
            {
                bool hasCp = await _context.CollectionPointAccounts
                    .AsNoTracking()
                    .AnyAsync(a => a.NetworkId == networkId.Value);
                if (!hasCp)
                {
                    ModelState.AddModelError(nameof(model.RecipientTarget), "لا توجد نقطة تحصيل مرتبطة بشبكتك حالياً.");
                }
                else if (!model.TargetCollectionPointAccountId.HasValue || model.TargetCollectionPointAccountId == 0)
                {
                    ModelState.AddModelError(nameof(model.TargetCollectionPointAccountId), "يرجى اختيار نقطة التحصيل.");
                }
                else
                {
                    bool cpOk = await _context.CollectionPointAccounts
                        .AsNoTracking()
                        .AnyAsync(a => a.Id == model.TargetCollectionPointAccountId.Value && a.NetworkId == networkId.Value);
                    if (!cpOk)
                    {
                        ModelState.AddModelError(nameof(model.TargetCollectionPointAccountId), "نقطة التحصيل المختارة غير مرتبطة بشبكتك.");
                    }
                }
            }

            PaymentMethod? pm = null;
            if (allowedIds.Contains(model.PaymentMethodId))
            {
                pm = await _context.PaymentMethods.AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == model.PaymentMethodId && m.IsActive);
            }

            bool isCash = pm?.IsCash == true;
            if (!isCash)
            {
                if (string.IsNullOrWhiteSpace(model.ReferenceNumber))
                {
                    ModelState.AddModelError(nameof(model.ReferenceNumber), "يرجى إدخال رقم الإشعار أو المرجع.");
                }
                if (model.ReceiptImage == null || model.ReceiptImage.Length == 0)
                {
                    ModelState.AddModelError(nameof(model.ReceiptImage), "يرجى رفع صورة الإيصال.");
                }
                else if (ImageUploadRules.IsTooLarge(model.ReceiptImage))
                {
                    ModelState.AddModelError(nameof(model.ReceiptImage), ImageUploadRules.MaxReceiptImageSizeMessage);
                }
            }

            ModelState.Remove(nameof(model.ReceiptImage));

            if (!ModelState.IsValid)
            {
                model.WalletBalance = client.Balance;
                model.ClientId = client.Id;
                model.PaymentMethodOptions = paymentOptions;
                model.CollectionPointOptions = await BuildCollectionPointOptionsAsync(networkId.Value);
                model.ShamCashPaymentMethodId = ResolveShamCashPaymentMethodId(paymentOptions);
                model.CompanyManagerShamCashQrCodePath = await ResolveCompanyManagerShamCashQrCodePathAsync(networkId.Value);
                return View(model);
            }

            string? receiptPath = null;
            if (!isCash && model.ReceiptImage != null && model.ReceiptImage.Length > 0)
            {
                string[] allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                string? ext = Path.GetExtension(model.ReceiptImage.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExtensions.Contains(ext))
                {
                    ModelState.AddModelError(nameof(model.ReceiptImage), "يرجى رفع صورة بصيغة مقبولة (JPG, PNG, GIF, WebP).");
                    model.WalletBalance = client.Balance;
                    model.ClientId = client.Id;
                    model.PaymentMethodOptions = paymentOptions;
                    model.CollectionPointOptions = await BuildCollectionPointOptionsAsync(networkId.Value);
                    model.ShamCashPaymentMethodId = ResolveShamCashPaymentMethodId(paymentOptions);
                    model.CompanyManagerShamCashQrCodePath = await ResolveCompanyManagerShamCashQrCodePathAsync(networkId.Value);
                    return View(model);
                }

                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (FileStream stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.ReceiptImage.CopyToAsync(stream);
                }

                receiptPath = $"/uploads/receipts/{uniqueFileName}";
            }

            ClientWalletTopUpRequest entity = new ClientWalletTopUpRequest
            {
                ClientId = client.Id,
                NetworkId = networkId.Value,
                RecipientTarget = model.RecipientTarget,
                TargetCollectionPointAccountId = model.RecipientTarget == ClientWalletTopUpRecipientTarget.CollectionPoint
                    ? model.TargetCollectionPointAccountId
                    : null,
                Amount = model.Amount,
                PaymentMethodId = model.PaymentMethodId,
                ReferenceNumber = string.IsNullOrWhiteSpace(model.ReferenceNumber) ? null : model.ReferenceNumber.Trim(),
                ReceiptImagePath = receiptPath,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                Status = ClientWalletTopUpRequestStatus.Pending,
                RequestedByUserId = user.Id,
                RequestedAt = DateTime.Now
            };

            _context.ClientWalletTopUpRequests.Add(entity);
            await _context.SaveChangesAsync();

            await _requestNotificationService.NotifyClientWalletTopUpRequestSubmittedAsync(
                entity,
                client.Name ?? client.UserName);

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(RequestTopUp));
        }

        private async Task<int?> ResolveClientNetworkIdAsync(int clientId)
        {
            Client? entity = await _context.Clients
                .AsNoTracking()
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == clientId);
            if (entity == null)
            {
                return null;
            }

            return entity.NetworkId ?? entity.MikroTikServer?.NetworkId;
        }

        private async Task<ClientPortalTopUpRequestViewModel> BuildTopUpRequestViewModelAsync(Client client, int networkId)
        {
            List<PaymentMethod> activeMethods = await _context.PaymentMethods
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Name)
                .ToListAsync();
            List<ClientPortalPaymentMethodOption> paymentOptions = BuildClientPortalPaymentMethodOptions(activeMethods);

            return new ClientPortalTopUpRequestViewModel
            {
                ClientId = client.Id,
                WalletBalance = client.Balance,
                PaymentMethodOptions = paymentOptions,
                CollectionPointOptions = await BuildCollectionPointOptionsAsync(networkId),
                ShamCashPaymentMethodId = ResolveShamCashPaymentMethodId(paymentOptions),
                CompanyManagerShamCashQrCodePath = await ResolveCompanyManagerShamCashQrCodePathAsync(networkId)
            };
        }

        private static List<ClientPortalPaymentMethodOption> BuildClientPortalPaymentMethodOptions(IReadOnlyList<PaymentMethod> active)
        {
            List<PaymentMethod> list = active.ToList();
            PaymentMethod? cash = list.FirstOrDefault(m => string.Equals(m.Name, "نقدي", StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(m => m.IsCash)
                ?? list.FirstOrDefault(m => string.Equals(m.Name, "كاش", StringComparison.OrdinalIgnoreCase));
            PaymentMethod? sham = list.FirstOrDefault(m => string.Equals(m.Name, "شام كاش", StringComparison.OrdinalIgnoreCase));
            PaymentMethod? bank = list.FirstOrDefault(m => string.Equals(m.Name, "بنك", StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(m => m.Name.StartsWith("بنك", StringComparison.OrdinalIgnoreCase));

            List<ClientPortalPaymentMethodOption> result = new List<ClientPortalPaymentMethodOption>();
            if (cash != null)
            {
                result.Add(new ClientPortalPaymentMethodOption { Id = cash.Id, Label = "نقدي", IsCash = cash.IsCash });
            }

            if (sham != null)
            {
                result.Add(new ClientPortalPaymentMethodOption { Id = sham.Id, Label = "شام كاش", IsCash = sham.IsCash });
            }

            if (bank != null)
            {
                result.Add(new ClientPortalPaymentMethodOption { Id = bank.Id, Label = "بنك", IsCash = bank.IsCash });
            }

            return result;
        }

        private async Task<List<ClientPortalCollectionPointOption>> BuildCollectionPointOptionsAsync(int networkId)
        {
            List<CollectionPointAccount> accounts = await _context.CollectionPointAccounts
                .AsNoTracking()
                .Include(a => a.User)
                .Where(a => a.NetworkId == networkId)
                .OrderBy(a => a.Id)
                .ToListAsync();

            return accounts.Select(a => new ClientPortalCollectionPointOption
            {
                CollectionPointAccountId = a.Id,
                DisplayName = a.User != null
                    ? (!string.IsNullOrWhiteSpace(a.User.FullName) ? a.User.FullName : a.User.UserName ?? $"#{a.Id}")
                    : $"نقطة تحصيل #{a.Id}",
                ShamCashQrCodePath = a.User?.ShamCashQrCodePath
            }).ToList();
        }

        private static int? ResolveShamCashPaymentMethodId(IReadOnlyCollection<ClientPortalPaymentMethodOption> options)
        {
            return options
                .FirstOrDefault(p => string.Equals(p.Label, "شام كاش", StringComparison.OrdinalIgnoreCase))
                ?.Id;
        }

        private async Task<string?> ResolveCompanyManagerShamCashQrCodePathAsync(int networkId)
        {
            return await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == networkId)
                .Select(n => n.ManagerUser != null ? n.ManagerUser.ShamCashQrCodePath : null)
                .FirstOrDefaultAsync();
        }

        /// <summary>صفحة تجديد الاشتراك (من المحفظة)</summary>
        public async Task<IActionResult> RenewSubscription()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            DateTime now = DateTime.Now;
            (decimal basePrice, decimal vatAmount, decimal amountDue) =
                await _vipPolicy.ApplyMonthlyPriceAsync(client);
            decimal vatPercentage = client.Profile?.VATPercentage ?? 0m;
            bool isExpired = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value < now;
            bool isExpiringSoon = client.AccountExpirationDate.HasValue &&
                                 client.AccountExpirationDate.Value >= now &&
                                 client.AccountExpirationDate.Value <= now.AddDays(7);
            RenewalBlockResult renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            bool canRenewFromWallet = amountDue > 0 && client.Balance >= amountDue && renewalGuard.CanRenew;

            List<RenewSubscriptionItemViewModel> dueSubscriptions = new List<RenewSubscriptionItemViewModel>
            {
                new RenewSubscriptionItemViewModel
                {
                    SubscriptionName = client.Profile?.Name ?? "اشتراك غير محدد",
                    ExpirationDate = client.AccountExpirationDate,
                    BasePrice = basePrice,
                    VatPercentage = vatPercentage,
                    VatAmount = vatAmount,
                    AmountDue = amountDue,
                    IsExpired = isExpired,
                    IsExpiringSoon = isExpiringSoon,
                    CanRenewFromWallet = canRenewFromWallet,
                    SubscriptionStatus = isExpired ? "منتهي" : (isExpiringSoon ? "ينتهي قريباً" : "ساري"),
                    PaymentStatus = amountDue <= 0
                        ? "غير متوفر"
                        : (!renewalGuard.CanRenew
                            ? "معلّق بسبب فواتير صيانة مستحقة"
                            : (canRenewFromWallet ? "جاهز للتجديد" : "بانتظار تغذية رصيد")),
                    IsPrimaryInternetSubscription = true
                }
            };

            RenewSubscriptionViewModel model = new RenewSubscriptionViewModel
            {
                ClientId = client.Id,
                WalletBalance = client.Balance,
                DueSubscriptions = dueSubscriptions,
                HasBlockingInvoices = !renewalGuard.CanRenew,
                BlockingInvoicesCount = renewalGuard.PendingInvoicesCount,
                BlockingInvoicesTotal = renewalGuard.TotalOutstanding
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MaintenanceInvoices()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            List<MaintenanceInvoice> invoices = await _context.MaintenanceInvoices
                .Where(i => i.ClientId == client.Id)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            ViewBag.ClientBalance = client.Balance;
            return View(invoices);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayMaintenanceInvoice(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            Client? client = await GetCurrentClientAsync();
            if (user == null || client == null)
            {
                TempData["Error"] = "تعذر التحقق من الحساب.";
                return RedirectToAction(nameof(Index));
            }

            MaintenanceInvoice? invoice = await _context.MaintenanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientId == client.Id);
            if (invoice == null)
            {
                TempData["Error"] = "الفاتورة غير موجودة.";
                return RedirectToAction(nameof(MaintenanceInvoices));
            }

            MaintenanceInvoicePaymentResult result = await _maintenanceBillingService.PayInvoiceFromClientWalletAsync(id, user.Id);
            if (!result.Success)
            {
                if (result.InsufficientBalance)
                {
                    TempData["Error"] =
                        $"رصيد المحفظة غير كافٍ لتسديد فاتورة الصيانة. المبلغ المطلوب: {result.RequiredAmount:N0} ل.س.";
                }
                else
                {
                    TempData["Error"] = result.ErrorMessage ?? "تعذر تسديد فاتورة الصيانة حالياً.";
                }

                return RedirectToAction(nameof(MaintenanceInvoices));
            }

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(MaintenanceInvoices));
        }

        /// <summary>بروفايل العميل — الاسم الكامل، البريد، رقم الجوال. اسم وكلمة مرور MikroTik للقراءة فقط.</summary>
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            return View(BuildClientProfileViewModel(client, user));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MyProfile(ClientProfileViewModel model)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }
            if (client.Id != model.ClientId)
            {
                TempData["Error"] = "غير مصرح بتعديل هذا الحساب";
                return RedirectToAction(nameof(Index));
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!ModelState.IsValid)
            {
                RestoreClientProfileReadOnlyFields(model, client, user);
                return View(model);
            }

            client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == client.Id);
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على الحساب";
                return RedirectToAction(nameof(Index));
            }

            string originalUserName = client.UserName ?? "";
            string? originalPassword = client.Password;

            client.Name = model.Name?.Trim();
            client.PhoneNumber = model.PhoneNumber?.Trim();
            client.ResidenceAddress = string.IsNullOrWhiteSpace(model.ResidenceAddress) ? null : model.ResidenceAddress.Trim();
            client.Latitude = model.Latitude;
            client.Longitude = model.Longitude;
            client.UserName = originalUserName;
            client.Password = originalPassword;
            client.LastUpdated = DateTime.Now;

            string? requestedEmail = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            if (!string.Equals(user.Email, requestedEmail, StringComparison.OrdinalIgnoreCase))
            {
                IdentityResult emailResult = await _userManager.SetEmailAsync(user, requestedEmail);
                if (!emailResult.Succeeded)
                {
                    foreach (IdentityError err in emailResult.Errors)
                    {
                        ModelState.AddModelError(nameof(model.Email), err.Description);
                    }

                    RestoreClientProfileReadOnlyFields(model, client, user);
                    return View(model);
                }
            }

            user.FullName = client.Name;
            user.PhoneNumber = client.PhoneNumber;
            user.LastUpdated = DateTime.Now;
            IdentityResult userUpdate = await _userManager.UpdateAsync(user);
            if (!userUpdate.Succeeded)
            {
                TempData["Error"] = string.Join(" | ", userUpdate.Errors.Select(e => e.Description));
                return RedirectToAction(nameof(MyProfile));
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToAction(nameof(MyProfile));
        }

        private static ClientProfileViewModel BuildClientProfileViewModel(Client client, ApplicationUser? user) =>
            new()
            {
                ClientId = client.Id,
                Name = client.Name ?? user?.FullName ?? "",
                Email = user?.Email,
                PhoneNumber = client.PhoneNumber ?? user?.PhoneNumber ?? "",
                ResidenceAddress = client.ResidenceAddress,
                Latitude = client.Latitude,
                Longitude = client.Longitude,
                SystemUserName = user?.UserName,
                MikroTikUserName = client.UserName,
                IsVip = client.IsVip,
                VipNote = client.VipNote,
                VipSince = client.VipSince
            };

        private static void RestoreClientProfileReadOnlyFields(
            ClientProfileViewModel model,
            Client client,
            ApplicationUser? user)
        {
            model.SystemUserName = user?.UserName;
            model.MikroTikUserName = client.UserName;
            model.IsVip = client.IsVip;
            model.VipNote = client.VipNote;
            model.VipSince = client.VipSince;
        }

        #endregion

        #region طلبات الصيانة

        /// <summary>
        /// قائمة طلبات الصيانة للعميل
        /// </summary>
        public async Task<IActionResult> MaintenanceRequests()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            List<MaintenanceRequest> requests = await _context.MaintenanceRequests
                .Where(m => m.ClientId == client.Id)
                .OrderByDescending(m => m.RequestDate)
                .ToListAsync();

            ViewBag.ClientName = client.Name;
            return View(requests);
        }

        /// <summary>
        /// صفحة إنشاء طلب صيانة جديد
        /// </summary>
        public async Task<IActionResult> CreateMaintenanceRequest()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            MaintenanceRequest model = new MaintenanceRequest
            {
                ClientId = client.Id,
                ContactPhone = client.PhoneNumber,
                Address = client.Receiver?.Name
            };

            await PopulateAssignableEmployeesAsync(client.Id, model.AssignedToId);
            return View(model);
        }

        /// <summary>
        /// إنشاء طلب صيانة جديد
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaintenanceRequest(MaintenanceRequest model)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            // التأكد من أن الطلب للعميل الحالي
            model.ClientId = client.Id;
            model.RequestDate = DateTime.Now;
            model.Status = MaintenanceRequestStatus.Pending;
            if (string.IsNullOrWhiteSpace(model.AssignedToId))
            {
                model.AssignedToId = null;
            }

            // إزالة حقول التحقق غير المطلوبة
            ModelState.Remove("Client");
            ModelState.Remove("AssignedTo");

            if (!string.IsNullOrWhiteSpace(model.AssignedToId))
            {
                int? companyNetworkId = await _maintenanceEmployeeTasks.ResolveCompanyNetworkIdForClientAsync(client.Id);
                if (!companyNetworkId.HasValue
                    || !await _maintenanceEmployeeTasks.IsAssignableEmployeeAsync(companyNetworkId.Value, model.AssignedToId))
                {
                    ModelState.AddModelError(nameof(model.AssignedToId), "الموظف المحدد غير متاح لإسناد مهمة الصيانة.");
                    model.AssignedToId = null;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.MaintenanceRequests.Add(model);
                    await _context.SaveChangesAsync();

                    await _maintenanceEmployeeTasks.EnsureTaskForAssignedMaintenanceAsync(model, assignedByUserId: null);

                    await _requestNotificationService.NotifyMaintenanceRequestSubmittedAsync(
                        model,
                        client.NetworkId,
                        client.Name,
                        client.UserName);

                    TempData["Success"] = AppMessages.OperationSuccess;
                    _logger.LogInformation($"تم إنشاء طلب صيانة جديد للعميل {client.Name} - النوع: {model.Type}");

                    return RedirectToAction(nameof(MaintenanceRequests));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إنشاء طلب الصيانة");
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تقديم الطلب. يرجى المحاولة مرة أخرى.");
                }
            }

            await PopulateAssignableEmployeesAsync(client.Id, model.AssignedToId);
            return View(model);
        }

        /// <summary>
        /// تفاصيل طلب صيانة
        /// </summary>
        public async Task<IActionResult> MaintenanceRequestDetails(int id)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            MaintenanceRequest? request = await _context.MaintenanceRequests
                .Include(m => m.AssignedTo)
                .FirstOrDefaultAsync(m => m.Id == id && m.ClientId == client.Id);

            if (request == null)
            {
                TempData["Error"] = "لم يتم العثور على الطلب";
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            return View(request);
        }

        /// <summary>
        /// إلغاء طلب صيانة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelMaintenanceRequest(int id)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على بيانات حسابك" });
            }

            MaintenanceRequest? request = await _context.MaintenanceRequests
                .FirstOrDefaultAsync(m => m.Id == id && m.ClientId == client.Id);

            if (request == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الطلب" });
            }

            // يمكن إلغاء الطلب فقط إذا كان في حالة انتظار
            if (request.Status != MaintenanceRequestStatus.Pending)
            {
                return Json(new { success = false, message = "لا يمكن إلغاء الطلب في هذه الحالة" });
            }

            request.Status = MaintenanceRequestStatus.Cancelled;
            await _context.SaveChangesAsync();
            await _maintenanceEmployeeTasks.CancelLinkedOpenTaskAsync(id);

            return Json(new { success = true, message = AppMessages.OperationSuccess });
        }

        #endregion

        #region طلبات تغيير السرعة

        /// <summary>
        /// قائمة طلبات تغيير السرعة للعميل
        /// </summary>
        public async Task<IActionResult> SpeedChangeRequests()
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            List<SpeedChangeRequest> requests = await _context.SpeedChangeRequests
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Where(s => s.ClientId == client.Id)
                .OrderByDescending(s => s.RequestDate)
                .ToListAsync();

            ViewBag.ClientName = client.Name;
            ViewBag.CurrentProfile = client.Profile;
            return View(requests);
        }

        /// <summary>
        /// صفحة إنشاء طلب تغيير سرعة
        /// </summary>
        public async Task<IActionResult> CreateSpeedChangeRequest()
        {
            return RedirectToAction(nameof(AvailablePlans));
        }

        /// <summary>
        /// إنشاء طلب تغيير سرعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSpeedChangeRequest(SpeedChangeRequest model)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            // التحقق من عدم وجود طلب معلق
            bool pendingRequest = await _context.SpeedChangeRequests
                .AnyAsync(s => s.ClientId == client.Id && s.Status == SpeedChangeRequestStatus.Pending);

            if (pendingRequest)
            {
                TempData["Warning"] = "لديك طلب تغيير سرعة معلق بالفعل.";
                return RedirectToAction(nameof(AvailablePlans));
            }

            // التأكد من أن الطلب للعميل الحالي
            model.ClientId = client.Id;
            model.CurrentProfileId = client.ProfileId;
            model.RequestDate = DateTime.Now;
            model.Status = SpeedChangeRequestStatus.Pending;

            // حساب فرق السعر + التحقق أن الباقة تابعة لنفس سيرفر المشترك
            Profile? requestedProfile = await _context.Profiles.FindAsync(model.RequestedProfileId);
            if (requestedProfile == null || !requestedProfile.IsActive)
            {
                TempData["Error"] = "الباقة المطلوبة غير موجودة أو غير مفعّلة.";
                return RedirectToAction(nameof(AvailablePlans));
            }

            if (!client.MikroTikServerId.HasValue ||
                requestedProfile.MikroTikServerId != client.MikroTikServerId.Value)
            {
                TempData["Error"] = "لا يمكن طلب باقة خارج السيرفر المرتبط باشتراكك.";
                return RedirectToAction(nameof(AvailablePlans));
            }

            if (client.NetworkId.HasValue &&
                requestedProfile.NetworkId.HasValue &&
                requestedProfile.NetworkId.Value != client.NetworkId.Value)
            {
                TempData["Error"] = "لا يمكن طلب باقة خارج شبكة اشتراكك.";
                return RedirectToAction(nameof(AvailablePlans));
            }

            if (client.Profile != null)
            {
                model.PriceDifference = requestedProfile.PriceWithVAT - client.Profile.PriceWithVAT;
            }

            // إزالة حقول التحقق غير المطلوبة
            ModelState.Remove("Client");
            ModelState.Remove("CurrentProfile");
            ModelState.Remove("RequestedProfile");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.SpeedChangeRequests.Add(model);
                    await _context.SaveChangesAsync();

                    await _requestNotificationService.NotifySpeedChangeRequestSubmittedAsync(
                        model,
                        client.Name,
                        client.Profile?.Name,
                        requestedProfile?.Name,
                        client.NetworkId);

                    TempData["Success"] = AppMessages.OperationSuccess;
                    _logger.LogInformation($"تم إنشاء طلب تغيير سرعة للعميل {client.Name} - من {client.Profile?.Name} إلى {requestedProfile?.Name}");

                    return RedirectToAction(nameof(AvailablePlans));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إنشاء طلب تغيير السرعة");
                    TempData["Error"] = "حدث خطأ أثناء تقديم الطلب. يرجى المحاولة مرة أخرى.";
                    return RedirectToAction(nameof(AvailablePlans));
                }
            }
            TempData["Error"] = "يرجى اختيار الباقة المطلوبة بشكل صحيح قبل إرسال الطلب.";
            return RedirectToAction(nameof(AvailablePlans));
        }

        /// <summary>
        /// تفاصيل طلب تغيير سرعة
        /// </summary>
        public async Task<IActionResult> SpeedChangeRequestDetails(int id)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            SpeedChangeRequest? request = await _context.SpeedChangeRequests
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Include(s => s.ProcessedBy)
                .FirstOrDefaultAsync(s => s.Id == id && s.ClientId == client.Id);

            if (request == null)
            {
                TempData["Error"] = "لم يتم العثور على الطلب";
                return RedirectToAction(nameof(SpeedChangeRequests));
            }

            return View(request);
        }

        /// <summary>
        /// إلغاء طلب تغيير سرعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSpeedChangeRequest(int id)
        {
            Client? client = await GetCurrentClientAsync();
            if (client == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على بيانات حسابك" });
            }

            SpeedChangeRequest? request = await _context.SpeedChangeRequests
                .FirstOrDefaultAsync(s => s.Id == id && s.ClientId == client.Id);

            if (request == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الطلب" });
            }

            // يمكن إلغاء الطلب فقط إذا كان في حالة انتظار
            if (request.Status != SpeedChangeRequestStatus.Pending)
            {
                return Json(new { success = false, message = "لا يمكن إلغاء الطلب في هذه الحالة" });
            }

            request.Status = SpeedChangeRequestStatus.Cancelled;
            await _context.SaveChangesAsync();

            return Json(new { success = true, message = AppMessages.OperationSuccess });
        }

        #endregion

        #region الباقات المتاحة

        /// <summary>
        /// عرض الباقات المتاحة للمشترك (فقط سرعات السيرفر التابع له).
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> AvailablePlans()
        {
            Client? currentClient = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentClient = await GetCurrentClientAsync();
            }

            IQueryable<Profile> profilesQuery = _context.Profiles
                .Where(p => p.IsActive && p.IsForNewClients);

            if (currentClient != null)
            {
                if (!currentClient.MikroTikServerId.HasValue)
                {
                    ViewBag.CurrentClient = currentClient;
                    ViewBag.CurrentProfileId = currentClient.ProfileId;
                    TempData["Warning"] = "حسابك غير مرتبط بسيرفر حالياً، لذلك لا يمكن عرض الباقات المتاحة.";
                    return View(new List<Profile>());
                }

                int serverId = currentClient.MikroTikServerId.Value;
                profilesQuery = profilesQuery.Where(p => p.MikroTikServerId == serverId);

                if (currentClient.NetworkId.HasValue)
                {
                    int networkId = currentClient.NetworkId.Value;
                    profilesQuery = profilesQuery.Where(p =>
                        p.NetworkId == null || p.NetworkId == networkId);
                }
            }

            List<Profile> profiles = await profilesQuery
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Price)
                .ToListAsync();

            ViewBag.CurrentClient = currentClient;
            ViewBag.CurrentProfileId = currentClient?.ProfileId;

            return View(profiles);
        }

        /// <summary>
        /// تفاصيل باقة معينة
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> PlanDetails(int id)
        {
            Profile? profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (profile == null)
            {
                TempData["Error"] = "الباقة غير موجودة";
                return RedirectToAction(nameof(AvailablePlans));
            }

            Client? currentClient = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentClient = await GetCurrentClientAsync();
            }

            if (currentClient != null &&
                currentClient.MikroTikServerId.HasValue &&
                profile.MikroTikServerId != currentClient.MikroTikServerId.Value)
            {
                TempData["Error"] = "هذه الباقة غير متاحة على السيرفر الخاص باشتراكك.";
                return RedirectToAction(nameof(AvailablePlans));
            }

            ViewBag.CurrentClient = currentClient;
            ViewBag.IsCurrentPlan = currentClient?.ProfileId == id;

            return View(profile);
        }

        /// <summary>
        /// مقارنة الباقات
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> ComparePlans(int[]? ids)
        {
            Client? currentClient = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentClient = await GetCurrentClientAsync();
            }

            IQueryable<Profile> profilesQuery = _context.Profiles
                .Where(p => p.IsActive && p.IsForNewClients);

            if (currentClient != null)
            {
                if (!currentClient.MikroTikServerId.HasValue)
                {
                    ViewBag.CurrentClient = currentClient;
                    ViewBag.CurrentProfileId = currentClient.ProfileId;
                    return View(new List<Profile>());
                }

                int serverId = currentClient.MikroTikServerId.Value;
                profilesQuery = profilesQuery.Where(p => p.MikroTikServerId == serverId);

                if (currentClient.NetworkId.HasValue)
                {
                    int networkId = currentClient.NetworkId.Value;
                    profilesQuery = profilesQuery.Where(p =>
                        p.NetworkId == null || p.NetworkId == networkId);
                }
            }

            List<Profile> profiles = await profilesQuery
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Price)
                .ToListAsync();

            // إذا تم تحديد باقات معينة للمقارنة
            if (ids != null && ids.Length > 0)
            {
                profiles = profiles.Where(p => ids.Contains(p.Id)).ToList();
            }

            ViewBag.CurrentClient = currentClient;
            ViewBag.CurrentProfileId = currentClient?.ProfileId;

            return View(profiles);
        }

        #endregion
    }
}
