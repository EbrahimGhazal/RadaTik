using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Services;
using RadTik.ViewModels.ClientPortal;

namespace RadTik.Controllers
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
        private readonly IMikroTikUsersService? _mikroTikService;
        private readonly RequestNotificationService _requestNotificationService;
        private readonly IMaintenanceBillingService _maintenanceBillingService;
        private readonly IClientRenewalGuardService _clientRenewalGuardService;
        private readonly IWebHostEnvironment _environment;

        public ClientPortalController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ClientPortalController> logger,
            RequestNotificationService requestNotificationService,
            IMaintenanceBillingService maintenanceBillingService,
            IClientRenewalGuardService clientRenewalGuardService,
            IWebHostEnvironment environment,
            IMikroTikUsersService? mikroTikService = null)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _requestNotificationService = requestNotificationService;
            _maintenanceBillingService = maintenanceBillingService;
            _clientRenewalGuardService = clientRenewalGuardService;
            _environment = environment;
            _mikroTikService = mikroTikService;
        }

        /// <summary>
        /// الحصول على العميل الحالي المرتبط بالمستخدم
        /// </summary>
        private async Task<Client?> GetCurrentClientAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.ClientId == null) return null;

            return await _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Receiver)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == user.ClientId);
        }

        #region لوحة التحكم الرئيسية

        /// <summary>
        /// الصفحة الرئيسية للعميل
        /// </summary>
        public async Task<IActionResult> Index()
        {
            var client = await GetCurrentClientAsync();
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
                var srv = client.MikroTikServer;
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
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Json(new { count = 0 });
            }

            var count = await _context.UserNotifications
                .AsNoTracking()
                .CountAsync(n => n.UserId == user.Id && !n.IsRead);

            return Json(new { count });
        }

        [HttpGet]
        public async Task<IActionResult> Notifications(bool unreadOnly = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var query = _context.UserNotifications
                .AsNoTracking()
                .Where(n => n.UserId == user.Id);

            if (unreadOnly)
            {
                query = query.Where(n => !n.IsRead);
            }

            var items = await query
                .OrderByDescending(n => n.CreatedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.UnreadOnly = unreadOnly;
            return View(items);
        }

        [HttpGet]
        public async Task<IActionResult> OpenNotification(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var row = await _context.UserNotifications
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

            var targetUrl = await ResolveClientNotificationTargetUrlAsync(row);
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
                    var invoiceId = TryParseNotificationEntityId(notification.Key);
                    if (invoiceId.HasValue)
                    {
                        var exists = await _context.MaintenanceInvoices
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
            var parts = (key ?? string.Empty).Split(':', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                return null;
            }

            return int.TryParse(parts[1], out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> MyTraffic()
        {
            var client = await GetCurrentClientAsync();
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

            var serverName = client.MikroTikServer?.Name;
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            client = await _context.Clients.Include(c => c.Profile).FirstOrDefaultAsync(c => c.Id == client.Id);
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على الحساب";
                return RedirectToAction(nameof(Index));
            }

            var basePrice = client.Profile?.Price ?? 0m;
            var vatPercentage = client.Profile?.VATPercentage ?? 0m;
            var vatAmount = basePrice * (vatPercentage / 100m);
            var amountDue = basePrice + vatAmount;
            if (amountDue <= 0)
            {
                TempData["Error"] = "لا يوجد سعر محدد للباقة. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                TempData["Error"] =
                    $"لا يمكنك تجديد الاشتراك حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
                return RedirectToAction(nameof(MaintenanceInvoices));
            }

            if (client.Balance < amountDue)
            {
                TempData["Error"] =
                    $"رصيد المحفظة غير كافٍ. المطلوب: {amountDue:N0} ل.س (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0})، ورصيدك: {client.Balance:N0} ل.س";
                return RedirectToAction(nameof(RenewSubscription));
            }

            try
            {
                client.Balance -= amountDue;
                var baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                    ? client.AccountExpirationDate.Value
                    : DateTime.Now;
                client.AccountExpirationDate = baseDate.AddMonths(1);
                client.LastUpdated = DateTime.Now;

                var wasStopped = !client.IsActive;

                if (_mikroTikService != null && client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    await _mikroTikService.RenewPPPoESubscription(
                        client.UserName,
                        client.MikroTikServerId.Value,
                        client.AccountExpirationDate.Value);
                }

                if (wasStopped)
                {
                    client.IsActive = true;
                    client.ConnectionStatus = "مفعل";
                }

                await _context.SaveChangesAsync();

                var msg = wasStopped
                    ? $"تم تجديد اشتراكك وإعادة تفعيل حسابك بنجاح. تم خصم {amountDue:N0} ل.س من محفظتك (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0}). الاشتراك حتى {client.AccountExpirationDate:yyyy/MM/dd}"
                    : $"تم تجديد اشتراكك بنجاح. تم خصم {amountDue:N0} ل.س من محفظتك (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0}). الاشتراك حتى {client.AccountExpirationDate:yyyy/MM/dd}";
                TempData["Success"] = msg;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في التجديد الذاتي للعميل {ClientId}", client.Id);
                TempData["Error"] = "حدث خطأ أثناء التجديد. يرجى المحاولة لاحقاً أو التواصل مع الإدارة.";
            }

            return RedirectToAction(nameof(RenewSubscription));
        }

        /// <summary>صفحة طلب تغذية رصيد المحفظة</summary>
        public async Task<IActionResult> RequestTopUp()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            var networkId = await ResolveClientNetworkIdAsync(client.Id);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حسابك بشبكة. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            var model = await BuildTopUpRequestViewModelAsync(client, networkId.Value);
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            var networkId = await ResolveClientNetworkIdAsync(client.Id);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "لم يتم ربط حسابك بشبكة. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Index));
            }

            var activeMethods = await _context.PaymentMethods
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Name)
                .ToListAsync();
            var paymentOptions = BuildClientPortalPaymentMethodOptions(activeMethods);
            var allowedIds = paymentOptions.Select(p => p.Id).ToHashSet();
            if (!allowedIds.Contains(model.PaymentMethodId))
            {
                ModelState.AddModelError(nameof(model.PaymentMethodId), "طريقة الدفع غير صالحة.");
            }

            if (model.RecipientTarget == ClientWalletTopUpRecipientTarget.CollectionPoint)
            {
                var hasCp = await _context.CollectionPointAccounts
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
                    var cpOk = await _context.CollectionPointAccounts
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

            var isCash = pm?.IsCash == true;
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
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(model.ReceiptImage.FileName)?.ToLowerInvariant();
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

                var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "receipts");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);
                var uniqueFileName = $"{Guid.NewGuid():N}{ext}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);
                await using (var stream = new FileStream(filePath, FileMode.Create))
                    await model.ReceiptImage.CopyToAsync(stream);
                receiptPath = $"/uploads/receipts/{uniqueFileName}";
            }

            var entity = new ClientWalletTopUpRequest
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

            TempData["Success"] = "تم إرسال طلب تغذية الرصيد بنجاح. سيتم مراجعته من الجهة المختارة.";
            return RedirectToAction(nameof(RequestTopUp));
        }

        private async Task<int?> ResolveClientNetworkIdAsync(int clientId)
        {
            var entity = await _context.Clients
                .AsNoTracking()
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == clientId);
            if (entity == null) return null;
            return entity.NetworkId ?? entity.MikroTikServer?.NetworkId;
        }

        private async Task<ClientPortalTopUpRequestViewModel> BuildTopUpRequestViewModelAsync(Client client, int networkId)
        {
            var activeMethods = await _context.PaymentMethods
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderBy(m => m.DisplayOrder)
                .ThenBy(m => m.Name)
                .ToListAsync();
            var paymentOptions = BuildClientPortalPaymentMethodOptions(activeMethods);

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
            var list = active.ToList();
            var cash = list.FirstOrDefault(m => string.Equals(m.Name, "نقدي", StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(m => m.IsCash)
                ?? list.FirstOrDefault(m => string.Equals(m.Name, "كاش", StringComparison.OrdinalIgnoreCase));
            var sham = list.FirstOrDefault(m => string.Equals(m.Name, "شام كاش", StringComparison.OrdinalIgnoreCase));
            var bank = list.FirstOrDefault(m => string.Equals(m.Name, "بنك", StringComparison.OrdinalIgnoreCase))
                ?? list.FirstOrDefault(m => m.Name.StartsWith("بنك", StringComparison.OrdinalIgnoreCase));

            var result = new List<ClientPortalPaymentMethodOption>();
            if (cash != null)
                result.Add(new ClientPortalPaymentMethodOption { Id = cash.Id, Label = "نقدي", IsCash = cash.IsCash });
            if (sham != null)
                result.Add(new ClientPortalPaymentMethodOption { Id = sham.Id, Label = "شام كاش", IsCash = sham.IsCash });
            if (bank != null)
                result.Add(new ClientPortalPaymentMethodOption { Id = bank.Id, Label = "بنك", IsCash = bank.IsCash });
            return result;
        }

        private async Task<List<ClientPortalCollectionPointOption>> BuildCollectionPointOptionsAsync(int networkId)
        {
            var accounts = await _context.CollectionPointAccounts
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            var now = DateTime.Now;
            var basePrice = client.Profile?.Price ?? 0m;
            var vatPercentage = client.Profile?.VATPercentage ?? 0m;
            var vatAmount = basePrice * (vatPercentage / 100m);
            var amountDue = basePrice + vatAmount;
            var isExpired = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value < now;
            var isExpiringSoon = client.AccountExpirationDate.HasValue &&
                                 client.AccountExpirationDate.Value >= now &&
                                 client.AccountExpirationDate.Value <= now.AddDays(7);
            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            var canRenewFromWallet = amountDue > 0 && client.Balance >= amountDue && renewalGuard.CanRenew;

            var dueSubscriptions = new List<RenewSubscriptionItemViewModel>
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

            var model = new RenewSubscriptionViewModel
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }

            var invoices = await _context.MaintenanceInvoices
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
            var user = await _userManager.GetUserAsync(User);
            var client = await GetCurrentClientAsync();
            if (user == null || client == null)
            {
                TempData["Error"] = "تعذر التحقق من الحساب.";
                return RedirectToAction(nameof(Index));
            }

            var invoice = await _context.MaintenanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.Id == id && i.ClientId == client.Id);
            if (invoice == null)
            {
                TempData["Error"] = "الفاتورة غير موجودة.";
                return RedirectToAction(nameof(MaintenanceInvoices));
            }

            var result = await _maintenanceBillingService.PayInvoiceFromClientWalletAsync(id, user.Id);
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

            TempData["Success"] = "تم تسديد فاتورة الصيانة بنجاح ويمكنك الآن متابعة التجديد.";
            return RedirectToAction(nameof(MaintenanceInvoices));
        }

        /// <summary>بروفايل العميل - تعديل الاسم الثلاثي، مكان السكن، رقم الجوال، والموقع على الخريطة</summary>
        [HttpGet]
        public async Task<IActionResult> MyProfile()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction(nameof(Index));
            }
            var model = new ClientProfileViewModel
            {
                ClientId = client.Id,
                Name = client.Name ?? "",
                PhoneNumber = client.PhoneNumber ?? "",
                ResidenceAddress = client.ResidenceAddress,
                Latitude = client.Latitude,
                Longitude = client.Longitude
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MyProfile(ClientProfileViewModel model)
        {
            var client = await GetCurrentClientAsync();
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

            if (ModelState.IsValid)
            {
                client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == client.Id);
                if (client == null)
                {
                    TempData["Error"] = "لم يتم العثور على الحساب";
                    return RedirectToAction(nameof(Index));
                }
                client.Name = model.Name?.Trim();
                client.PhoneNumber = model.PhoneNumber?.Trim();
                client.ResidenceAddress = string.IsNullOrWhiteSpace(model.ResidenceAddress) ? null : model.ResidenceAddress.Trim();
                client.Latitude = model.Latitude;
                client.Longitude = model.Longitude;
                client.LastUpdated = DateTime.Now;
                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حفظ بيانات البروفايل بنجاح.";
                return RedirectToAction(nameof(MyProfile));
            }

            return View(model);
        }

        #endregion

        #region طلبات الصيانة

        /// <summary>
        /// قائمة طلبات الصيانة للعميل
        /// </summary>
        public async Task<IActionResult> MaintenanceRequests()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            var requests = await _context.MaintenanceRequests
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            var model = new MaintenanceRequest
            {
                ClientId = client.Id,
                ContactPhone = client.PhoneNumber,
                Address = client.Receiver?.Name
            };

            return View(model);
        }

        /// <summary>
        /// إنشاء طلب صيانة جديد
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateMaintenanceRequest(MaintenanceRequest model)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            // التأكد من أن الطلب للعميل الحالي
            model.ClientId = client.Id;
            model.RequestDate = DateTime.Now;
            model.Status = MaintenanceRequestStatus.Pending;

            // إزالة حقول التحقق غير المطلوبة
            ModelState.Remove("Client");

            if (ModelState.IsValid)
            {
                try
                {
                    _context.MaintenanceRequests.Add(model);
                    await _context.SaveChangesAsync();

                    await _requestNotificationService.NotifyMaintenanceRequestSubmittedAsync(
                        model,
                        client.NetworkId,
                        client.Name,
                        client.UserName);

                    TempData["Success"] = "✅ تم تقديم طلب الصيانة بنجاح. سيتم مراجعته من قبل الفريق الفني.";
                    _logger.LogInformation($"تم إنشاء طلب صيانة جديد للعميل {client.Name} - النوع: {model.Type}");

                    return RedirectToAction(nameof(MaintenanceRequests));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في إنشاء طلب الصيانة");
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تقديم الطلب. يرجى المحاولة مرة أخرى.");
                }
            }

            return View(model);
        }

        /// <summary>
        /// تفاصيل طلب صيانة
        /// </summary>
        public async Task<IActionResult> MaintenanceRequestDetails(int id)
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            var request = await _context.MaintenanceRequests
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على بيانات حسابك" });
            }

            var request = await _context.MaintenanceRequests
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

            return Json(new { success = true, message = "تم إلغاء الطلب بنجاح" });
        }

        #endregion

        #region طلبات تغيير السرعة

        /// <summary>
        /// قائمة طلبات تغيير السرعة للعميل
        /// </summary>
        public async Task<IActionResult> SpeedChangeRequests()
        {
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            var requests = await _context.SpeedChangeRequests
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            // التحقق من عدم وجود طلب معلق
            var pendingRequest = await _context.SpeedChangeRequests
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

            // حساب فرق السعر
            var requestedProfile = await _context.Profiles.FindAsync(model.RequestedProfileId);
            if (requestedProfile != null && client.Profile != null)
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

                    TempData["Success"] = "✅ تم تقديم طلب تغيير السرعة بنجاح. سيتم مراجعته من قبل الإدارة.";
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على بيانات حسابك";
                return RedirectToAction("Index", "Home");
            }

            var request = await _context.SpeedChangeRequests
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
            var client = await GetCurrentClientAsync();
            if (client == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على بيانات حسابك" });
            }

            var request = await _context.SpeedChangeRequests
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

            return Json(new { success = true, message = "تم إلغاء الطلب بنجاح" });
        }

        #endregion

        #region الباقات المتاحة

        /// <summary>
        /// عرض جميع الباقات المتاحة مع تفاصيلها
        /// </summary>
        [AllowAnonymous]
        public async Task<IActionResult> AvailablePlans()
        {
            var profiles = await _context.Profiles
                .Where(p => p.IsActive && p.IsForNewClients)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Price)
                .ToListAsync();

            // إذا كان المستخدم مسجل دخول وعميل، نحصل على باقته الحالية
            Client? currentClient = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentClient = await GetCurrentClientAsync();
            }

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
            var profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);

            if (profile == null)
            {
                TempData["Error"] = "الباقة غير موجودة";
                return RedirectToAction(nameof(AvailablePlans));
            }

            // إذا كان المستخدم مسجل دخول وعميل، نحصل على باقته الحالية
            Client? currentClient = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentClient = await GetCurrentClientAsync();
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
            var profiles = await _context.Profiles
                .Where(p => p.IsActive && p.IsForNewClients)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Price)
                .ToListAsync();

            // إذا تم تحديد باقات معينة للمقارنة
            if (ids != null && ids.Length > 0)
            {
                profiles = profiles.Where(p => ids.Contains(p.Id)).ToList();
            }

            // إذا كان المستخدم مسجل دخول وعميل، نحصل على باقته الحالية
            Client? currentClient = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                currentClient = await GetCurrentClientAsync();
            }

            ViewBag.CurrentClient = currentClient;
            ViewBag.CurrentProfileId = currentClient?.ProfileId;

            return View(profiles);
        }

        #endregion
    }
}
