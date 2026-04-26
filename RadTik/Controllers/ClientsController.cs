using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Services;
using RadTik.Helpers;
using RadTik.Security;
using System.Threading.Tasks;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RadTik.Controllers
{
    // CompanyEmployee هو الدور الجديد للموظف التابع للشركة، و EmployeeLegacy للتوافق مع الحسابات القديمة.
    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator,CompanyEmployee,Employee,Client")]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Clients)]
    public class ClientsController : Controller
    {
        private const string ContractTemplateServiceKey = "CONTRACT_TEMPLATE";
        private const string ContractMetaServiceKey = "CONTRACT_META";

        private readonly ApplicationDbContext _context;
        private readonly IMikroTikUsersService _mikroTikService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ClientsController> _logger;
        private readonly PermissionService _permissionService;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly RequestNotificationService _requestNotificationService;
        private readonly IClientRenewalGuardService _clientRenewalGuardService;

        private static readonly string DefaultContractBodyHtml = @"
<p>تم إبرام هذا العقد بين كل من:</p>
<p><strong>الطرف الأول:</strong> شركة/شبكة {{NetworkName}} ويُشار إليها لاحقاً بـ (الشركة).</p>
<p><strong>الطرف الثاني:</strong> المشترك السيد/السيدة {{SubscriberName}} رقم المشترك {{SubscriberNumber}}، ويُشار إليه لاحقاً بـ (المشترك).</p>
<p>اتفق الطرفان على تزويد المشترك بخدمة الاتصال وفق الباقة المعتمدة {{ProfileName}} ابتداءً من تاريخ الاشتراك {{SubscriptionStartDate}}.</p>
<p>يلتزم المشترك بسداد الرسوم الدورية في مواعيدها، والمحافظة على تجهيزات الخدمة، وعدم إساءة استخدام الاتصال بما يخالف الأنظمة المعمول بها.</p>
<p>تحتفظ الشركة بحقها في تحديث الإجراءات الفنية والتنظيمية بما يضمن جودة الخدمة واستمراريتها.</p>
<p>يُعد توقيع المشترك أدناه موافقة صريحة على بنود هذا العقد.</p>";

        public ClientsController(
            ApplicationDbContext context, 
            IMikroTikUsersService mikroTikService, 
            UserManager<ApplicationUser> userManager,
            ILogger<ClientsController> logger,
            PermissionService permissionService,
            IUsageBasedSubscriptionChargeService usageChargeService,
            RequestNotificationService requestNotificationService,
            IClientRenewalGuardService clientRenewalGuardService)
        {
            _context = context;
            _mikroTikService = mikroTikService;
            _userManager = userManager;
            _logger = logger;
            _permissionService = permissionService;
            _usageChargeService = usageChargeService;
            _requestNotificationService = requestNotificationService;
            _clientRenewalGuardService = clientRenewalGuardService;
        }

        // GET: Clients
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            IQueryable<Client> query = _context.Clients
                .Include(c => c.Receiver)
                    .ThenInclude(r => r!.Sector)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Profile);

            // إذا كان المستخدم عميل فقط (وليس موظف أو مدير)، يعرض بياناته فقط
            bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy);
            bool isClientOnly = userRoles.Contains(RoleNames.Client) && !isEmployee && !userRoles.Contains(RoleNames.NetworkAdministrator);
            if (isClientOnly)
            {
                if (user.ClientId != null)
                {
                    query = query.Where(c => c.Id == user.ClientId);
                }
                else
                {
                    query = query.Where(c => false); // لا يعرض شيئاً
                }
            }
            else
            {
                // للموظفين/المديرين عند عرض قائمة العملاء: نحتاج صلاحية عرض العملاء
                var canView = await _permissionService.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }

                // للموظفين ومديري الشبكة: تصفية حسب الشبكة المحددة
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (networkId.HasValue)
                {
                    query = query.Where(c => c.NetworkId == networkId.Value);
                }
                else
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }
            }

            var clients = await query.ToListAsync();
            var clientIds = clients.Select(c => c.Id).ToList();
            var dbAccountMap = await _context.Users
                .Where(u => u.ClientId.HasValue && clientIds.Contains(u.ClientId.Value))
                .Select(u => new { ClientId = u.ClientId!.Value, u.UserName })
                .ToDictionaryAsync(x => x.ClientId, x => x.UserName ?? string.Empty);
            var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (selectedNetworkId.HasValue)
            {
                ViewBag.PendingClientIds = await GetPendingClientIdsAsync(selectedNetworkId.Value);
            }
            else
            {
                ViewBag.PendingClientIds = new HashSet<int>();
            }

            ViewBag.DbAccountMap = dbAccountMap;
            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = selectedNetworkId;
            return View(clients);
        }

        // GET: Clients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            // إذا كان المستخدم عميل فقط، يمكنه فقط رؤية بياناته
            bool isEmployee = userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy);
            bool isClientOnly = userRoles.Contains(RoleNames.Client) && !isEmployee && !userRoles.Contains(RoleNames.NetworkAdministrator);
            
            if (isClientOnly)
            {
                // التأكد من أن العميل يحاول الوصول إلى حسابه فقط
                if (user?.ClientId == null || user.ClientId != id.Value)
                {
                    return Forbid(); // رفض الوصول
                }
            }
            else
            {
                var canView = await _permissionService.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }
            }

            var client = await _context.Clients
                .Include(c => c.Receiver)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Profile)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (client == null)
            {
                return NotFound();
            }

            ViewBag.IsPendingClientApproval = await IsPendingClientApprovalAsync(client);

            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                ViewBag.RenewalBlockedMessage =
                    $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
            }

            // المايكروتك: حصراً لمدير الشركة (وليس للموظفين/العملاء)
            if (User.IsInRole(RoleNames.NetworkAdministrator) && client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                try
                {
                    var mikrotikInfo = await _mikroTikService.GetPPPoEUserInfo(client.UserName, client.MikroTikServerId.Value);
                    if (mikrotikInfo != null)
                    {
                        ViewBag.MikroTikInfo = mikrotikInfo;
                    }
                }
                catch (Exception ex)
                {
                    ViewBag.MikroTikError = BuildFriendlyMikroTikErrorMessage("تعذر جلب بيانات MikroTik", ex.Message);
                }
            }
            else
            {
                // لا نسمح بالوصول إلى MikroTik (للموظف/العميل)
                ViewBag.MikroTikInfo = null;
                ViewBag.IsClientView = true;
            }

            ViewBag.IsClientOnly = isClientOnly;

            // تغذية الرصيد: آخر عمليات التغذية
            ViewBag.RecentTopUps = await _context.ClientTopUpTransactions
                .Where(t => t.ClientId == client.Id)
                .OrderByDescending(t => t.CreatedAt)
                .Take(10)
                .Include(t => t.CreatedByUser)
                .ToListAsync();

            return View(client);
        }

        /// <summary>
        /// نقطة توافق قديمة: موعد التركيب أصبح مرتبطاً تلقائياً بتاريخ إضافة العميل (CreatedDate).
        /// هذه العملية لا تحفظ أي قيمة جديدة وتعيد رسالة توضيحية فقط.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> SetScheduledInstallationDate(int id, DateTime? scheduledInstallationDate)
        {
            _ = scheduledInstallationDate;
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client == null)
            {
                return NotFound();
            }

            TempData["Info"] = $"موعد التركيب للعميل «{client.Name ?? client.UserName ?? client.Id.ToString()}» مرتبط تلقائياً بتاريخ الإضافة: {client.CreatedDate:yyyy/MM/dd HH:mm}.";
            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Clients/MembershipContract/5
        [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
        public async Task<IActionResult> MembershipContract(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var canView = await _permissionService.HasPermissionAsync(User, "Clients.View");
            if (!canView && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                return Forbid();
            }

            var query = _context.Clients
                .Include(c => c.Profile)
                .Include(c => c.Network)
                .Include(c => c.Receiver)
                .AsQueryable();

            if (User.IsInRole(RoleNames.NetworkAdministrator) && !User.IsInRole(RoleNames.SystemAdministrator))
            {
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                query = query.Where(c => c.NetworkId == networkId.Value);
            }

            var client = await query.FirstOrDefaultAsync(c => c.Id == id);
            if (client == null)
            {
                return NotFound();
            }

            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                TempData["Error"] =
                    $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
                return RedirectToAction(nameof(Details), new { id });
            }

            var contractNetworkId = client.NetworkId ?? 0;
            var meta = contractNetworkId > 0 ? await GetContractMetaAsync(contractNetworkId) : new ContractMetaSettings();
            var templateBody = contractNetworkId > 0 ? await GetContractTemplateBodyAsync(contractNetworkId) : DefaultContractBodyHtml;
            var renderedContractBody = RenderContractTemplate(templateBody, client, DateTime.Now);

            ViewBag.ContractDate = DateTime.Now;
            ViewBag.ContractTitle = string.IsNullOrWhiteSpace(meta.ContractTitle) ? "عقد انضمام إلى الشركة / الشبكة" : meta.ContractTitle;
            ViewBag.ContractRecordNumber = string.IsNullOrWhiteSpace(meta.RecordNumber) ? "-" : meta.RecordNumber;
            ViewBag.ContractLicenseNumber = string.IsNullOrWhiteSpace(meta.LicenseNumber) ? "-" : meta.LicenseNumber;
            ViewBag.ContractBodyHtml = renderedContractBody;

            return View(client);
        }

        [HttpGet]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        public async Task<IActionResult> ContractTemplateSettings()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var network = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
            if (network == null)
            {
                TempData["Error"] = "تعذر العثور على الشبكة الحالية.";
                return RedirectToAction(nameof(Index));
            }

            var meta = await GetContractMetaAsync(network.Id);
            var body = await GetContractTemplateBodyAsync(network.Id);

            ViewBag.AvailableVariables = GetContractVariableMap();
            ViewBag.VariableSyntaxHint = "اكتب المتغير بهذا الشكل: {{VariableName}}";
            ViewBag.PreviewHtml = RenderContractTemplate(
                body,
                new Client
                {
                    Name = "اسم مشترك تجريبي",
                    SID = "000000",
                    UserName = "test-user",
                    CreatedDate = DateTime.Today,
                    ServiceStartDate = DateTime.Today,
                    AccountExpirationDate = DateTime.Today.AddMonths(1),
                    Profile = new Profile { Name = "باقة تجريبية" },
                    Network = network
                },
                DateTime.Now);

            ViewBag.ContractTitle = string.IsNullOrWhiteSpace(meta.ContractTitle) ? "عقد انضمام إلى الشركة / الشبكة" : meta.ContractTitle;
            ViewBag.RecordNumber = meta.RecordNumber;
            ViewBag.LicenseNumber = meta.LicenseNumber;
            ViewBag.ContractBodyTemplate = body;
            ViewBag.DefaultContractBodyTemplate = DefaultContractBodyHtml;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        public async Task<IActionResult> ContractTemplateSettings(string contractTitle, string? recordNumber, string? licenseNumber, string contractBodyTemplate)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            var network = await _context.Networks.FirstOrDefaultAsync(n => n.Id == networkId.Value);
            if (network == null)
            {
                TempData["Error"] = "تعذر العثور على الشبكة الحالية.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(contractTitle))
            {
                ModelState.AddModelError("contractTitle", "عنوان العقد مطلوب.");
            }
            if (string.IsNullOrWhiteSpace(contractBodyTemplate))
            {
                ModelState.AddModelError("contractBodyTemplate", "نص العقد مطلوب.");
            }

            var unknownVariables = FindUnknownTemplateVariables(contractBodyTemplate, GetContractVariableMap().Keys);
            if (unknownVariables.Count > 0)
            {
                ModelState.AddModelError("contractBodyTemplate", $"يوجد متغيرات غير معروفة داخل النص: {string.Join(", ", unknownVariables)}");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.AvailableVariables = GetContractVariableMap();
                ViewBag.VariableSyntaxHint = "اكتب المتغير بهذا الشكل: {{VariableName}}";
                ViewBag.ContractTitle = contractTitle;
                ViewBag.RecordNumber = recordNumber;
                ViewBag.LicenseNumber = licenseNumber;
                ViewBag.ContractBodyTemplate = contractBodyTemplate;
                ViewBag.DefaultContractBodyTemplate = DefaultContractBodyHtml;
                ViewBag.PreviewHtml = RenderContractTemplate(
                    contractBodyTemplate ?? string.Empty,
                    new Client
                    {
                        Name = "اسم مشترك تجريبي",
                        SID = "000000",
                        UserName = "test-user",
                        CreatedDate = DateTime.Today,
                        ServiceStartDate = DateTime.Today,
                        AccountExpirationDate = DateTime.Today.AddMonths(1),
                        Profile = new Profile { Name = "باقة تجريبية" },
                        Network = network
                    },
                    DateTime.Now);
                return View();
            }

            var meta = new ContractMetaSettings
            {
                ContractTitle = contractTitle.Trim(),
                RecordNumber = string.IsNullOrWhiteSpace(recordNumber) ? null : recordNumber.Trim(),
                LicenseNumber = string.IsNullOrWhiteSpace(licenseNumber) ? null : licenseNumber.Trim()
            };

            await UpsertCustomServiceItemAsync(network.Id, ContractMetaServiceKey, "إعدادات ميتا عقد الانضمام", JsonSerializer.Serialize(meta));
            await UpsertCustomServiceItemAsync(network.Id, ContractTemplateServiceKey, "قالب نص عقد الانضمام", contractBodyTemplate.Trim());

            TempData["Success"] = "تم حفظ إعدادات عقد الانضمام بنجاح.";
            return RedirectToAction(nameof(ContractTemplateSettings));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.NetworkAdministrator},{RoleNames.SystemAdministrator}")]
        public async Task<IActionResult> ResetContractTemplateToDefault()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction(nameof(Index));
            }

            await UpsertCustomServiceItemAsync(networkId.Value, ContractTemplateServiceKey, "قالب نص عقد الانضمام", DefaultContractBodyHtml);
            TempData["Success"] = "تمت إعادة ضبط نص العقد إلى القالب الافتراضي.";
            return RedirectToAction(nameof(ContractTemplateSettings));
        }

        /// <summary>تغذية رصيد العميل - من مدير النظام أو مدير الشبكة</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = $"{RoleNames.SystemAdministrator},{RoleNames.NetworkAdministrator}")]
        public async Task<IActionResult> TopUpBalance(int id, decimal amount, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login", "Account");

            var client = await _context.Clients.Include(c => c.Network).FirstOrDefaultAsync(c => c.Id == id);
            if (client == null) return NotFound();

            if (amount < 0.01m)
            {
                TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var isSystemAdmin = User.IsInRole(RoleNames.SystemAdministrator);
            var isNetworkManager = User.IsInRole(RoleNames.NetworkAdministrator);

            if (!isSystemAdmin && !isNetworkManager)
            {
                TempData["Error"] = "غير مصرح بتغذية الرصيد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ClientTopUpSource sourceType;
            if (isSystemAdmin)
            {
                sourceType = ClientTopUpSource.SystemAdmin;
            }
            else
            {
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!networkId.HasValue || client.NetworkId != networkId.Value)
                {
                    TempData["Error"] = "لا يمكن تغذية رصيد عميل من شبكة أخرى.";
                    return RedirectToAction(nameof(Details), new { id });
                }
                sourceType = ClientTopUpSource.NetworkManager;
            }

            using var tx = await _context.Database.BeginTransactionAsync();
            try
            {
                var prevBalance = client.Balance;
                client.Balance += amount;
                client.LastUpdated = DateTime.Now;

                if (sourceType == ClientTopUpSource.NetworkManager && client.NetworkId.HasValue)
                {
                    var network = await _context.Networks.FindAsync(client.NetworkId.Value);
                    if (network == null) { await tx.RollbackAsync(); TempData["Error"] = "لم يتم العثور على الشبكة."; return RedirectToAction(nameof(Details), new { id }); }
                    if (network.Balance < amount)
                    {
                        await tx.RollbackAsync();
                        TempData["Error"] = $"رصيد الشبكة غير كافٍ. الرصيد الحالي: {network.Balance:N0} ل.س";
                        return RedirectToAction(nameof(Details), new { id });
                    }
                    var prevNetworkBalance = network.Balance;
                    network.Balance -= amount;

                    _context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                    {
                        NetworkId = network.Id,
                        Type = NetworkWalletTransactionType.Adjustment,
                        SignedAmount = -amount,
                        PreviousBalance = prevNetworkBalance,
                        NewBalance = network.Balance,
                        CreatedByUserId = user.Id,
                        CreatedAt = DateTime.Now,
                        Notes = $"تغذية رصيد عميل #{client.Id} ({client.UserName})"
                    });
                }

                _context.ClientTopUpTransactions.Add(new ClientTopUpTransaction
                {
                    ClientId = client.Id,
                    Amount = amount,
                    PreviousBalance = prevBalance,
                    NewBalance = client.Balance,
                    SourceType = sourceType,
                    CreatedByUserId = user.Id,
                    Notes = notes?.Trim(),
                    NetworkId = sourceType == ClientTopUpSource.NetworkManager ? client.NetworkId : null
                });

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                await _requestNotificationService.NotifyClientTopUpSubmittedAsync(
                    client.Id,
                    client.NetworkId,
                    amount,
                    sourceType == ClientTopUpSource.SystemAdmin ? "مدير النظام" : "مدير الشبكة",
                    user.FullName ?? user.UserName);

                var sourceName = sourceType == ClientTopUpSource.SystemAdmin ? "مدير النظام" : "مدير الشبكة";
                TempData["Success"] = $"تم تغذية رصيد العميل بمبلغ {amount:N0} ل.س من {sourceName}. الرصيد الحالي: {client.Balance:N0} ل.س";
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "خطأ في تغذية رصيد العميل {ClientId}", id);
                TempData["Error"] = "حدث خطأ أثناء تغذية الرصيد.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        /// <summary>تجديد الاشتراك ذاتياً من قبل المشترك: حسم سعر الباقة من رصيد محفظته وتمديد شهر</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleNames.Client)]
        public async Task<IActionResult> SelfRenewSubscription(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.ClientId != id)
            {
                TempData["Error"] = "غير مصرح بتجديد هذا الحساب.";
                return RedirectToAction("Index", "Home");
            }

            var client = await _context.Clients
                .Include(c => c.Profile)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (client == null)
            {
                TempData["Error"] = "لم يتم العثور على الحساب.";
                return RedirectToAction("Index", "Home");
            }

            var basePrice = client.Profile?.Price ?? 0m;
            var vatPercentage = client.Profile?.VATPercentage ?? 0m;
            var vatAmount = basePrice * (vatPercentage / 100m);
            var amountDue = basePrice + vatAmount;
            if (amountDue <= 0)
            {
                TempData["Error"] = "لا يوجد سعر محدد للباقة. يرجى التواصل مع الإدارة.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var renewalGuard = await _clientRenewalGuardService.CheckBlockingInvoicesAsync(client.Id);
            if (!renewalGuard.CanRenew)
            {
                TempData["Error"] =
                    $"لا يمكن تنفيذ التجديد حالياً قبل تسديد جميع فواتير الصيانة المستحقة (عدد الفواتير: {renewalGuard.PendingInvoicesCount}، إجمالي المستحقات: {renewalGuard.TotalOutstanding:N0} ل.س).";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (client.Balance < amountDue)
            {
                TempData["Error"] =
                    $"رصيد المحفظة غير كافٍ. المطلوب: {amountDue:N0} ل.س (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0})، ورصيدك: {client.Balance:N0} ل.س";
                return RedirectToAction(nameof(Details), new { id });
            }

            try
            {
                client.Balance -= amountDue;
                var baseDate = client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now
                    ? client.AccountExpirationDate.Value
                    : DateTime.Now;
                client.AccountExpirationDate = baseDate.AddMonths(1);
                client.LastRenewalDate = DateTime.Now.Date;
                client.LastUpdated = DateTime.Now;

                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    await _mikroTikService.RenewPPPoESubscription(
                        client.UserName,
                        client.MikroTikServerId.Value,
                        client.AccountExpirationDate.Value);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] =
                    $"تم تجديد اشتراكك بنجاح. تم خصم {amountDue:N0} ل.س من محفظتك (السعر الأساسي: {basePrice:N0} + الضريبة {vatPercentage:N2}%: {vatAmount:N0}). الاشتراك حتى {client.AccountExpirationDate:yyyy/MM/dd}";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في التجديد الذاتي للعميل {ClientId}", id);
                TempData["Error"] = "حدث خطأ أثناء التجديد. يرجى المحاولة لاحقاً أو التواصل مع الإدارة.";
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Clients/Create
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = new Client
            {
                ServiceStartDate = DateTime.Now.Date,
                AccountExpirationDate = DateTime.Now.Date.AddMonths(1)
            };
            await PrepareViewDataForCreate(client);

            return View(client);
        }

        // POST: Clients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Create")]
        public async Task<IActionResult> Create([Bind("Id,Name,SID,UserName,Password,ProfileId,PhoneNumber,ResidenceAddress,Latitude,Longitude,PowerSource,Building,Floor,IsActive,ReceiverId,Service,Address,MikroTikServerId,ServiceStartDate,AccountExpirationDate")] Client client, string? dbUserName, string? dbPassword)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            if (!ModelState.IsValid)
            {
                await PrepareViewDataForCreate(client);
                return View(client);
            }

            if (!ValidateClientData(client))
            {
                await PrepareViewDataForCreate(client);
                return View(client);
            }

            var userRoles = user != null
                ? await _userManager.GetRolesAsync(user)
                : Array.Empty<string>();
            var isEmployee = (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy)) &&
                             !userRoles.Contains(RoleNames.NetworkAdministrator);

            if (isEmployee && user != null)
            {
                var profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == client.ProfileId && p.NetworkId == networkId.Value);
                if (profile == null)
                {
                    ModelState.AddModelError("ProfileId", "البروفايل المحدد غير موجود في هذه الشبكة");
                    await PrepareViewDataForCreate(client);
                    return View(client);
                }

                if (client.ReceiverId.HasValue && client.ReceiverId.Value <= 0)
                {
                    client.ReceiverId = null;
                }

                var selectedServerId = client.MikroTikServerId;
                var payload = new ClientApprovalPayload
                {
                    Name = client.Name,
                    SID = client.SID,
                    UserName = client.UserName,
                    Password = client.Password,
                    ProfileId = client.ProfileId,
                    ProfileName = profile.Name,
                    PhoneNumber = client.PhoneNumber,
                    ResidenceAddress = client.ResidenceAddress,
                    Latitude = client.Latitude,
                    Longitude = client.Longitude,
                    PowerSource = client.PowerSource,
                    Building = client.Building,
                    Floor = client.Floor,
                    ReceiverId = client.ReceiverId,
                    MikroTikServerId = selectedServerId,
                    ServiceStartDate = client.ServiceStartDate,
                    AccountExpirationDate = client.AccountExpirationDate,
                    DbUserName = dbUserName,
                    DbPassword = dbPassword
                };

                client.ProfileName = profile.Name;
                client.NetworkId = networkId.Value;
                client.CreatedDate = DateTime.Now;
                client.LastUpdated = DateTime.Now;
                client.IsActive = false;
                client.ConnectionStatus = "معلق بانتظار موافقة مدير الشركة";
                if (!client.AccountExpirationDate.HasValue)
                {
                    client.AccountExpirationDate = DateTime.Now.AddMonths(1);
                }
                if (!client.ServiceStartDate.HasValue)
                {
                    client.ServiceStartDate = DateTime.Now.Date;
                }
                client.LastRenewalDate = DateTime.Now.Date;

                _context.Clients.Add(client);
                await _context.SaveChangesAsync();

                var requestNotes = EmployeeApprovalRequestHelper.BuildClientCreate(client.Id, payload);
                if (string.IsNullOrWhiteSpace(requestNotes))
                {
                    _context.Clients.Remove(client);
                    await _context.SaveChangesAsync();
                    ModelState.AddModelError(string.Empty, "تعذر إنشاء طلب الموافقة: حجم البيانات كبير جداً.");
                    await PrepareViewDataForCreate(client);
                    return View(client);
                }

                await CreateEmployeeApprovalRequestAsync(
                    networkId.Value,
                    user.Id,
                    FeatureKeys.Clients,
                    requestNotes,
                    await ResolveExpectedClientCreateChargeAsync(networkId.Value));

                TempData["Info"] = "تم تسجيل إضافة العميل كطلب موافقة. سيتم إنشاؤه على النظام والمايكروتك بعد اعتماد مدير الشركة.";
                return RedirectToAction(nameof(Index));
            }

            if (ModelState.IsValid)
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                bool mikroTikSuccess = false;
                ApplicationUser? newUser = null;

                try
                {
                    _logger.LogInformation($"🚀 بدء عملية إضافة عميل جديد: {client.UserName}");

                    // ربط العميل بالشبكة
                    client.NetworkId = networkId.Value;

                    var selectedNetwork = await _context.Networks
                        .AsNoTracking()
                        .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                    var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                    var clientChargeEstimate = await _usageChargeService.EstimateImportChargeAsync(
                        companyNetworkId,
                        PricingChargeUnit.PerSubscriber,
                        1);
                    var requiredAmount = clientChargeEstimate.RequiredAmountSyp;
                    if (requiredAmount > 0m)
                    {
                        var walletBalance = clientChargeEstimate.WalletBalance;
                        if (walletBalance < requiredAmount)
                        {
                            throw new Exception($"رصيد محفظة الشركة غير كافٍ لإضافة عميل جديد. المطلوب {requiredAmount:N2} ل.س.ج والرصيد الحالي {walletBalance:N2} ل.س.ج.");
                        }
                    }

                    // التحقق من عدم وجود اسم مستخدم مكرر
                    var existingUser = await _userManager.FindByNameAsync(client.UserName!);
                    if (existingUser != null)
                    {
                        throw new Exception("اسم المستخدم موجود مسبقاً في النظام");
                    }

                    // الحصول على البروفايل من قاعدة البيانات
                    var profile = await _context.Profiles
                        .FirstOrDefaultAsync(p => p.Id == client.ProfileId && p.NetworkId == networkId.Value);
                    if (profile == null)
                    {
                        throw new Exception("البروفايل المحدد غير موجود في هذه الشبكة");
                    }

                    // تعيين ProfileName للتوافق مع MikroTikService
                    client.ProfileName = profile.Name;

                    // السماح بترك المستقبل فارغاً (مشترك مباشر على المايكروتك)
                    if (client.ReceiverId.HasValue && client.ReceiverId.Value <= 0)
                    {
                        client.ReceiverId = null;
                    }

                    // الخطوة 1: إضافة المستخدم في المايكروتك أولاً
                    if (client.MikroTikServerId.HasValue)
                    {
                        try
                        {
                            await _mikroTikService.AddPPPoEUser(client);
                            mikroTikSuccess = true;
                            _logger.LogInformation($"✅ تم إضافة المستخدم {client.UserName} في المايكروتك بنجاح");
                        }
                        catch (Exception mikroTikEx)
                        {
                            _logger.LogError(mikroTikEx, "❌ فشل إضافة المستخدم في المايكروتك: {ErrorMessage}", mikroTikEx.Message);
                            throw new Exception(BuildFriendlyMikroTikErrorMessage("فشل الإضافة في المايكروتك", mikroTikEx.Message));
                        }
                    }

                    // الخطوة 2: إضافة العميل في قاعدة البيانات
                    try
                    {
                        client.CreatedDate = DateTime.Now;
                        client.LastUpdated = DateTime.Now;
                        client.ConnectionStatus = client.IsActive ? "مفعل" : "معطل";
                        
                        // إذا لم يتم تحديد تاريخ انتهاء الصلاحية، يتم تعيينه بعد شهر من تاريخ الإنشاء
                        if (!client.AccountExpirationDate.HasValue)
                        {
                            client.AccountExpirationDate = DateTime.Now.AddMonths(1);
                        }
                        if (!client.ServiceStartDate.HasValue)
                        {
                            client.ServiceStartDate = DateTime.Now.Date;
                        }
                        client.LastRenewalDate = DateTime.Now.Date;

                        _context.Add(client);
                        await _context.SaveChangesAsync();
                        _logger.LogInformation($"✅ تم إضافة العميل {client.UserName} في قاعدة البيانات بنجاح");
                    }
                    catch (DbUpdateException dbEx)
                    {
                        _logger.LogError(dbEx, "❌ فشل حفظ العميل في قاعدة البيانات: {ErrorMessage}", dbEx.Message);

                        // إذا فشل حفظ قاعدة البيانات ولكن نجحت إضافة المايكروتك، نقوم بالتنظيف
                        if (mikroTikSuccess)
                        {
                            await CleanupFailedCreation(client);
                        }

                        throw new Exception($"فشل حفظ البيانات في قاعدة البيانات: {dbEx.InnerException?.Message ?? dbEx.Message}");
                    }

                    // الخطوة 3: إنشاء حساب مستخدم للعميل في النظام
                    try
                    {
                        // إذا كان اسم المستخدم يبدو كإيميل حقيقي، نستخدمه كما هو
                        // وإلا نكوّن بريد داخلي افتراضي
                        var normalizedDbUserName = string.IsNullOrWhiteSpace(dbUserName) ? client.UserName : dbUserName.Trim();
                        var normalizedDbPassword = string.IsNullOrWhiteSpace(dbPassword) ? client.Password : dbPassword.Trim();

                        string userEmail;
                        if (!string.IsNullOrWhiteSpace(normalizedDbUserName) && normalizedDbUserName.Contains("@"))
                        {
                            userEmail = normalizedDbUserName;
                        }
                        else
                        {
                            userEmail = $"{normalizedDbUserName}@radtik.local";
                        }

                        newUser = new ApplicationUser
                        {
                            UserName = normalizedDbUserName,
                            Email = userEmail, // بريد افتراضي أو نفس اسم المستخدم إذا كان بريداً
                            FullName = client.Name,
                            PhoneNumber = client.PhoneNumber,
                            CreatedDate = DateTime.Now,
                            IsActive = client.IsActive,
                            ClientId = client.Id, // ربط المستخدم بالعميل
                            NetworkId = networkId.Value
                        };

                        var createResult = await _userManager.CreateAsync(newUser, normalizedDbPassword!);
                        if (!createResult.Succeeded)
                        {
                            var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                            throw new Exception($"فشل إنشاء حساب المستخدم: {errors}");
                        }

                        // إضافة دور العميل
                        await _userManager.AddToRoleAsync(newUser, "Client");
                        _logger.LogInformation($"✅ تم إنشاء حساب مستخدم للعميل {client.UserName} بنجاح");
                    }
                    catch (Exception userEx)
                    {
                        _logger.LogError(userEx, "❌ فشل إنشاء حساب المستخدم: {ErrorMessage}", userEx.Message);
                        
                        // تنظيف: حذف العميل من قاعدة البيانات
                        _context.Clients.Remove(client);
                        await _context.SaveChangesAsync();
                        
                        // تنظيف: حذف من المايكروتك
                        if (mikroTikSuccess)
                        {
                            await CleanupFailedCreation(client);
                        }

                        throw new Exception($"فشل إنشاء حساب المستخدم: {userEx.Message}");
                    }

                    // كل شيء نجح، نؤكد العملية
                    await transaction.CommitAsync();

                    await _usageChargeService.ChargeUsageIncreaseAsync(companyNetworkId, user!.Id, PricingChargeUnit.PerSubscriber);

                    TempData["Success"] = "✅ تم إضافة العميل بنجاح في قاعدة البيانات والمايكروتك وإنشاء حساب له في النظام";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    ModelState.AddModelError(string.Empty, $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في الإضافة", ex.Message)}");
                    _logger.LogError(ex, "❌ فشل عملية إضافة العميل {UserName}", client.UserName);
                }
            }

            await PrepareViewDataForCreate(client);
            return View(client);
        }

        // GET: Clients/Edit/5
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.Profile)
                .Include(c => c.Receiver)
                    .ThenInclude(r => r!.Sector)
                .FirstOrDefaultAsync(c => c.Id == id);
            
            if (client == null)
            {
                return NotFound();
            }

            // التحقق من صلاحيات الموظف
            var userRoles = await _userManager.GetRolesAsync(currentUser!);
            bool isEmployee = (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy)) &&
                              !userRoles.Contains(RoleNames.NetworkAdministrator);

            await PrepareViewDataForEdit(client);
            var linkedUser = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.ClientId == client.Id);
            ViewBag.DbUserName = linkedUser?.UserName ?? client.UserName;
            ViewBag.IsEmployee = isEmployee;
            return View(client);
        }

        // POST: Clients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.Edit")]
        public async Task<IActionResult> Edit(
            int id,
            [Bind("Id,Name,SID,UserName,Password,ProfileId,PhoneNumber,ResidenceAddress,Latitude,Longitude,PowerSource,Building,Floor,ServiceStartDate,CreatedDate,IsActive,ReceiverId,Service,Address,Uptime,ConnectionStatus,MacAddress,MikroTikServerId,AccountExpirationDate")] Client client,
            string? dbUserName,
            string? dbPassword)
        {
            if (id != client.Id)
            {
                return NotFound();
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            // التحقق من أن العميل يتبع الشبكة المحددة
            var existingClient = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (existingClient == null)
            {
                return NotFound();
            }

            // التحقق من صلاحيات الموظف
            var userRoles = await _userManager.GetRolesAsync(currentUser!);
            bool isEmployee = (userRoles.Contains(RoleNames.CompanyEmployee) || userRoles.Contains(RoleNames.EmployeeLegacy)) &&
                              !userRoles.Contains(RoleNames.NetworkAdministrator);

            // جلب البيانات الأصلية للعميل (من نفس الشبكة)
            var originalClient = await _context.Clients
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (originalClient == null)
            {
                return NotFound();
            }

            // مدير الشركة يمكنه تعديل: كلمة المرور، البروفايل، الاسم الثلاثي، السيرفر، اللاقط (المرسل عبر المستقبل)
            // الموظف يمكنه فقط: الاسم، كلمة المرور، اسم المستخدم، المستقبل (اللاقط)
            if (isEmployee)
            {
                client.ProfileId = originalClient.ProfileId;
                client.ProfileName = originalClient.ProfileName;
                client.IsActive = originalClient.IsActive;
                client.MikroTikServerId = originalClient.MikroTikServerId;
                client.AccountExpirationDate = originalClient.AccountExpirationDate;
                client.Service = originalClient.Service;
                client.Address = originalClient.Address;
                client.Uptime = originalClient.Uptime;
                client.ConnectionStatus = originalClient.ConnectionStatus;
                client.MacAddress = originalClient.MacAddress;
                client.Balance = originalClient.Balance;
                client.ServiceStartDate = originalClient.ServiceStartDate;
                client.ServiceEndDate = originalClient.ServiceEndDate;
                client.NextBillingDate = originalClient.NextBillingDate;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    if (isEmployee && currentUser != null)
                    {
                        var payload = new ClientApprovalPayload
                        {
                            Name = client.Name,
                            UserName = client.UserName,
                            Password = string.IsNullOrWhiteSpace(client.Password) ? null : client.Password,
                            PhoneNumber = client.PhoneNumber,
                            ResidenceAddress = client.ResidenceAddress,
                            Latitude = client.Latitude,
                            Longitude = client.Longitude,
                            PowerSource = client.PowerSource,
                            Building = client.Building,
                            Floor = client.Floor,
                            ReceiverId = client.ReceiverId,
                            DbUserName = dbUserName,
                            DbPassword = dbPassword
                        };

                        var requestNotes = EmployeeApprovalRequestHelper.BuildClientEdit(existingClient.Id, payload);
                        if (string.IsNullOrWhiteSpace(requestNotes))
                        {
                            ModelState.AddModelError(string.Empty, "تعذر إنشاء طلب الموافقة: حجم البيانات كبير جداً.");
                            await PrepareViewDataForEdit(client);
                            ViewBag.DbUserName = string.IsNullOrWhiteSpace(dbUserName) ? client.UserName : dbUserName;
                            ViewBag.IsEmployee = true;
                            return View(client);
                        }

                        await CreateEmployeeApprovalRequestAsync(
                            networkId.Value,
                            currentUser.Id,
                            FeatureKeys.Clients,
                            requestNotes,
                            0m);

                        TempData["Info"] = "تم إرسال تعديل العميل كطلب موافقة لمدير الشركة.";
                        return RedirectToAction(nameof(Index));
                    }

                    var profile = await _context.Profiles.FindAsync(client.ProfileId);
                    if (profile == null)
                    {
                        throw new Exception("البروفايل المحدد غير موجود");
                    }

                    // نسخ القيم المسموح بها من النموذج إلى الكيان المتتبع فقط (تفادي خطأ التتبع)
                    existingClient.Name = client.Name;
                    existingClient.UserName = client.UserName;
                    existingClient.PhoneNumber = client.PhoneNumber;
                    existingClient.ProfileId = client.ProfileId;
                    existingClient.ProfileName = profile.Name;
                    existingClient.ReceiverId = client.ReceiverId;
                    existingClient.MikroTikServerId = client.MikroTikServerId;
                    existingClient.AccountExpirationDate = client.AccountExpirationDate;
                    existingClient.ResidenceAddress = client.ResidenceAddress;
                    existingClient.Latitude = client.Latitude;
                    existingClient.Longitude = client.Longitude;
                    existingClient.PowerSource = client.PowerSource;
                    existingClient.Building = client.Building;
                    existingClient.Floor = client.Floor;
                    existingClient.ServiceStartDate = client.ServiceStartDate;
                    existingClient.LastUpdated = DateTime.Now;
                    existingClient.NetworkId = networkId.Value;

                    if (!string.IsNullOrWhiteSpace(client.Password))
                    {
                        existingClient.Password = client.Password;
                    }

                    if (!isEmployee)
                    {
                        existingClient.IsActive = client.IsActive;
                        existingClient.Service = client.Service;
                        existingClient.Address = client.Address;
                        existingClient.ConnectionStatus = client.IsActive ? "مفعل" : "معطل";
                    }

                    var originalServerId = originalClient.MikroTikServerId;
                    var newServerId = existingClient.MikroTikServerId;
                    var originalUserName = originalClient.UserName;
                    var userNameChanged = !string.Equals(originalClient.UserName, existingClient.UserName, StringComparison.Ordinal);

                    if (originalServerId.HasValue && newServerId.HasValue && originalServerId.Value != newServerId.Value)
                    {
                        // تغيير السيرفر: إذا المستخدم غير موجود على السيرفر الجديد يتم إنشاؤه، وإلا يتم تحديثه.
                        var existsOnNewServer = await _mikroTikService.CheckUserExists(existingClient.UserName!, newServerId.Value);
                        if (!existsOnNewServer)
                        {
                            await _mikroTikService.AddPPPoEUser(existingClient);
                        }
                        else
                        {
                            await _mikroTikService.UpdatePPPoEUser(existingClient);
                        }

                        await _context.SaveChangesAsync();
                        if (!string.IsNullOrEmpty(originalUserName))
                        {
                            await _mikroTikService.DeletePPPoEUser(originalUserName!, originalServerId.Value);
                        }
                    }
                    else
                    {
                        if (existingClient.MikroTikServerId.HasValue)
                        {
                            if (userNameChanged)
                            {
                                await _mikroTikService.UpdatePPPoEUserWithOriginalUsername(existingClient, originalUserName ?? string.Empty);
                            }
                            else
                            {
                                await _mikroTikService.UpdatePPPoEUser(existingClient);
                            }
                        }
                        await _context.SaveChangesAsync();
                    }

                    // تحديث حساب Identity المرتبط بالعميل
                    var linkedUser = await _context.Users.FirstOrDefaultAsync(u => u.ClientId == existingClient.Id);
                    if (linkedUser != null)
                    {
                        var normalizedDbUserName = string.IsNullOrWhiteSpace(dbUserName) ? linkedUser.UserName : dbUserName.Trim();
                        if (!string.IsNullOrWhiteSpace(normalizedDbUserName) && !string.Equals(linkedUser.UserName, normalizedDbUserName, StringComparison.Ordinal))
                        {
                            var setUserNameResult = await _userManager.SetUserNameAsync(linkedUser, normalizedDbUserName);
                            if (!setUserNameResult.Succeeded)
                            {
                                _logger.LogWarning($"⚠️ فشل تحديث اسم المستخدم في النظام: {string.Join(", ", setUserNameResult.Errors.Select(e => e.Description))}");
                            }
                        }
                        if (!string.IsNullOrWhiteSpace(dbPassword))
                        {
                            var token = await _userManager.GeneratePasswordResetTokenAsync(linkedUser);
                            var resetResult = await _userManager.ResetPasswordAsync(linkedUser, token, dbPassword.Trim());
                            if (!resetResult.Succeeded)
                            {
                                _logger.LogWarning($"⚠️ فشل تحديث كلمة المرور في النظام: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
                            }
                        }
                        linkedUser.FullName = existingClient.Name;
                        linkedUser.PhoneNumber = existingClient.PhoneNumber;
                        if (!isEmployee)
                        {
                            linkedUser.IsActive = existingClient.IsActive;
                        }
                        await _userManager.UpdateAsync(linkedUser);
                    }

                    TempData["Success"] = "✅ تم تعديل بيانات العميل بنجاح في قاعدة البيانات والمايكروتك";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError(string.Empty, $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في التعديل", ex.Message)}");
                    _logger.LogError(ex, "خطأ في تعديل عميل");
                }
            }

            await PrepareViewDataForEdit(client);
            ViewBag.DbUserName = string.IsNullOrWhiteSpace(dbUserName) ? client.UserName : dbUserName;
            ViewBag.IsEmployee = isEmployee;
            return View(client);
        }

        // GET: Clients/Delete/5
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.Receiver)
                    .ThenInclude(r => r!.Sector)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Profile)
                .FirstOrDefaultAsync(m => m.Id == id);
            
            if (client == null)
            {
                return NotFound();
            }

            // التحقق مما إذا كان المشترك موجوداً على المايكروتك لعرض رسالة تأكيد مناسبة
            bool existsOnMikroTik = false;
            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                try
                {
                    existsOnMikroTik = await _mikroTikService.CheckUserExists(client.UserName, client.MikroTikServerId.Value);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "خطأ أثناء التحقق من وجود المستخدم {UserName} على المايكروتك", client.UserName);
                }
            }

            ViewBag.ExistsOnMikroTik = existsOnMikroTik;
            return View(client);
        }

        // POST: Clients/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            if (client != null)
            {
                try
                {
                    // الخطوة 1: حذف المستخدم من المايكروتك
                    if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                    {
                        await _mikroTikService.DeletePPPoEUser(client.UserName, client.MikroTikServerId.Value);
                    }

                    // الخطوة 2: حذف العميل من قاعدة البيانات
                    _context.Clients.Remove(client);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "✅ تم حذف العميل بنجاح من قاعدة البيانات والمايكروتك";
                }
                catch (Exception ex)
                {
                    TempData["Error"] = BuildFriendlyMikroTikErrorMessage(
                        "تم حذف العميل من قاعدة البيانات ولكن حدث خطأ في حذفه من المايكروتك",
                        ex.Message);
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Clients/ToggleStatus/5
        [HttpPost]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            
            if (client != null)
            {
                client.IsActive = !client.IsActive;
                client.LastUpdated = DateTime.Now;
                _context.Update(client);
                await _context.SaveChangesAsync();

                var status = client.IsActive ? "مفعل" : "معطل";
                TempData["Success"] = $"تم {status} العميل بنجاح";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Clients/Freeze/5 - تجميد الحساب
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee,EmployeeLegacy")]
        public async Task<IActionResult> Freeze(int id)
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canEdit = await _permissionService.HasPermissionAsync(User, "Clients.Edit");
                if (!canEdit)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            try
            {
                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    // تجميد الحساب في المايكروتك
                    await _mikroTikService.FreezeAccount(client.MikroTikServerId.Value, client.UserName);
                    TempData["Success"] = "✅ تم تجميد الإنترنت للمشترك على المايكروتك فقط";
                }
                else
                {
                    TempData["Error"] = "❌ لا يمكن تجميد الحساب: لم يتم تحديد خادم المايكروتك أو اسم المستخدم";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجميد الحساب", ex.Message)}";
                _logger.LogError(ex, "خطأ في تجميد الحساب للعميل {UserName}", client.UserName);
            }

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(referer))
            {
                return Redirect(referer);
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        // POST: Clients/Unfreeze/5 - تفعيل الحساب
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee,EmployeeLegacy")]
        public async Task<IActionResult> Unfreeze(int id)
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canEdit = await _permissionService.HasPermissionAsync(User, "Clients.Edit");
                if (!canEdit)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            try
            {
                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    // تفعيل الحساب في المايكروتك
                    await _mikroTikService.UnfreezeAccount(client.MikroTikServerId.Value, client.UserName);
                    TempData["Success"] = "✅ تم تفعيل الحساب بنجاح";
                }
                else
                {
                    TempData["Error"] = "❌ لا يمكن تفعيل الحساب: لم يتم تحديد خادم المايكروتك أو اسم المستخدم";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تفعيل الحساب", ex.Message)}";
                _logger.LogError(ex, "خطأ في تفعيل الحساب للعميل {UserName}", client.UserName);
            }

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(referer))
            {
                return Redirect(referer);
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        // POST: Clients/RenewOneMonth/5 - تجديد شهر من تاريخ الانتهاء الحالي
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee,EmployeeLegacy")]
        public async Task<IActionResult> RenewOneMonth(int id)
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canEdit = await _permissionService.HasPermissionAsync(User, "Clients.Edit");
                if (!canEdit)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            try
            {
                var baseDate = client.AccountExpirationDate?.Date ?? DateTime.Now.Date;
                var newExpirationDate = baseDate.AddMonths(1).AddDays(-1);

                if (client.MikroTikServerId.HasValue && !string.IsNullOrWhiteSpace(client.UserName))
                {
                    await _mikroTikService.RenewPPPoESubscription(
                        client.UserName,
                        client.MikroTikServerId.Value,
                        newExpirationDate);
                }

                client.AccountExpirationDate = newExpirationDate;
                client.LastRenewalDate = DateTime.Now.Date;
                client.LastUpdated = DateTime.Now;
                _context.Update(client);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ تم تجديد الاشتراك لمدة شهر حتى تاريخ {newExpirationDate:yyyy/MM/dd}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجديد الاشتراك", ex.Message)}";
                _logger.LogError(ex, "خطأ في التجديد الشهري للعميل {UserName}", client.UserName);
            }

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrWhiteSpace(referer))
            {
                return Redirect(referer);
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Clients/RenewSubscription/5
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RenewSubscription(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .FirstOrDefaultAsync(c => c.Id == id && c.NetworkId == networkId.Value);
            
            if (client == null)
            {
                return NotFound();
            }

            ViewBag.ClientId = client.Id;
            ViewBag.ClientName = client.Name;
            ViewBag.CurrentExpirationDate = client.AccountExpirationDate;

            return View();
        }

        // POST: Clients/RenewSubscription/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RenewSubscription(int id, DateTime? expirationDate, int? renewDays)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            DateTime newExpirationDate;

            // إذا تم تحديد عدد الأيام للتجديد
            if (renewDays.HasValue && renewDays.Value > 0)
            {
                newExpirationDate = DateTime.Now.AddDays(renewDays.Value);
            }
            // إذا تم تحديد تاريخ محدد
            else if (expirationDate.HasValue)
            {
                newExpirationDate = expirationDate.Value;
            }
            else
            {
                TempData["Error"] = "❌ يجب تحديد تاريخ انتهاء الصلاحية أو عدد الأيام للتجديد";
                return RedirectToAction(nameof(Details), new { id = id });
            }

            try
            {
                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    // تحديث تاريخ انتهاء الصلاحية في المايكروتك
                    await _mikroTikService.RenewPPPoESubscription(
                        client.UserName,
                        client.MikroTikServerId.Value,
                        newExpirationDate);

                    // تحديث تاريخ انتهاء الصلاحية في قاعدة البيانات
                    client.AccountExpirationDate = newExpirationDate;
                    client.LastRenewalDate = DateTime.Now.Date;
                    client.LastUpdated = DateTime.Now;
                    _context.Update(client);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"✅ تم تجديد الاشتراك بنجاح حتى تاريخ {newExpirationDate:yyyy/MM/dd}";
                }
                else
                {
                    TempData["Error"] = "❌ لا يمكن التجديد: لم يتم تحديد خادم المايكروتك أو اسم المستخدم";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجديد الاشتراك", ex.Message)}";
                _logger.LogError(ex, "خطأ في تجديد الاشتراك للعميل {UserName}", client.UserName);
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        // POST: Clients/RenewTo8thNextMonth/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> RenewTo8thNextMonth(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            try
            {
                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    // تجديد حتى تاريخ 8 من الشهر القادم
                    await _mikroTikService.RenewSubscriptionTo8thNextMonth(
                        client.UserName,
                        client.MikroTikServerId.Value);

                    // حساب تاريخ 8 من الشهر القادم
                    var today = DateTime.Now;
                    var nextMonth = today.AddMonths(1);
                    var renewalDate = new DateTime(nextMonth.Year, nextMonth.Month, 8);

                    // تحديث تاريخ انتهاء الصلاحية في قاعدة البيانات
                    client.AccountExpirationDate = renewalDate;
                    client.LastRenewalDate = DateTime.Now.Date;
                    client.LastUpdated = DateTime.Now;
                    _context.Update(client);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = $"✅ تم تجديد الاشتراك بنجاح حتى تاريخ {renewalDate:yyyy/MM/dd} (8 من الشهر القادم)";
                }
                else
                {
                    TempData["Error"] = "❌ لا يمكن التجديد: لم يتم تحديد خادم المايكروتك أو اسم المستخدم";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في تجديد الاشتراك", ex.Message)}";
                _logger.LogError(ex, "خطأ في تجديد الاشتراك للعميل {UserName}", client.UserName);
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        // GET: Clients/SyncWithMikroTik/5
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> SyncWithMikroTik(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            try
            {
                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    await _mikroTikService.UpdatePPPoEUser(client);
                    TempData["Success"] = "✅ تم مزامنة البيانات مع المايكروتك بنجاح";
                }
                else
                {
                    TempData["Error"] = "❌ لا يمكن المزامنة: لم يتم تحديد خادم المايكروتك أو اسم المستخدم";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في المزامنة مع المايكروتك", ex.Message)}";
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        // GET: Clients/CheckExpiredAccounts
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> CheckExpiredAccounts()
        {
            try
            {
                var result = await _mikroTikService.CheckAndDisableExpiredAccounts();

                if (result.Success)
                {
                    if (result.DisabledAccounts.Count > 0)
                    {
                        TempData["Success"] = $"✅ {result.Message} - تم إيقاف {result.DisabledAccounts.Count} حساب";
                    }
                    else
                    {
                        TempData["Info"] = $"✅ {result.Message} - لا توجد حسابات منتهية الصلاحية";
                    }
                }
                else
                {
                    TempData["Error"] = $"❌ {result.Message}";
                }

                // إذا كان هناك حسابات متوقفة، يمكن عرضها
                if (result.DisabledAccounts.Count > 0)
                {
                    TempData["ExpiredAccounts"] = System.Text.Json.JsonSerializer.Serialize(result.DisabledAccounts);
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في التحقق من الحسابات المنتهية", ex.Message)}";
                _logger.LogError(ex, "خطأ في التحقق من الحسابات المنتهية");
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Clients/ImportFromServer - صفحة اختيار السيرفر واستيراد المشتركين
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.ImportFromServer")]
        public async Task<IActionResult> ImportFromServer()
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canView = await _permissionService.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .OrderBy(s => s.Name)
                .ToListAsync();

            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId.Value);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            var importPreviewByServer = new Dictionary<int, ImportUsersPreviewResult>();
            var importChargeByServer = new Dictionary<int, UsageImportChargeEstimate>();
            foreach (var server in servers)
            {
                var preview = await _mikroTikService.BuildUsersImportPreviewAsync(server.Id, networkId.Value);
                var subscriberEstimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSubscriber,
                    preview.ImportableUsersCount);

                var estimate = new UsageImportChargeEstimate
                {
                    ImportableCount = preview.ImportableUsersCount,
                    MatchedPricingsCount = subscriberEstimate.MatchedPricingsCount,
                    UnitPriceSyp = subscriberEstimate.UnitPriceSyp,
                    RequiredAmountSyp = subscriberEstimate.RequiredAmountSyp,
                    WalletBalance = subscriberEstimate.WalletBalance
                };
                importPreviewByServer[server.Id] = preview;
                importChargeByServer[server.Id] = estimate;
            }
            var baseSubscriberUnitEstimate = await _usageChargeService.EstimateImportChargeAsync(
                companyNetworkId,
                PricingChargeUnit.PerSubscriber,
                1);

            ViewBag.ImportPreviewByServer = importPreviewByServer;
            ViewBag.ImportChargeByServer = importChargeByServer;
            ViewBag.ClientImportUnitPrice = baseSubscriberUnitEstimate.UnitPriceSyp;

            ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            ViewBag.CurrentNetworkId = networkId;
            return View(servers);
        }

        // POST: Clients/ImportFromServer - تنفيذ الاستيراد من السيرفر المحدد
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        [RequirePermission("Clients.ImportFromServer")]
        public async Task<IActionResult> ImportFromServer(int serverId)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

            if (server == null)
            {
                TempData["Error"] = "السيرفر غير موجود أو لا يتبع الشبكة الحالية";
                return RedirectToAction(nameof(ImportFromServer));
            }

            try
            {
                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                var preview = await _mikroTikService.BuildUsersImportPreviewAsync(serverId, networkId.Value);
                if (preview.ImportableUsersCount <= 0)
                {
                    TempData["Error"] = "لا يوجد عملاء جدد قابلين للاستيراد من هذا السيرفر حالياً.";
                    return RedirectToAction(nameof(ImportFromServer));
                }

                var subscriberEstimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSubscriber,
                    preview.ImportableUsersCount);
                var requiredAmount = subscriberEstimate.RequiredAmountSyp;
                var walletBalance = subscriberEstimate.WalletBalance;

                if (requiredAmount > 0m && walletBalance < requiredAmount)
                {
                    TempData["Error"] =
                        $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({walletBalance:N2}) أقل من المبلغ المطلوب ({requiredAmount:N2}) ل.س.ج.";
                    return RedirectToAction(nameof(ImportFromServer));
                }

                var result = await _mikroTikService.ImportAllUsersToDatabase(serverId, networkId.Value);

                if (result.Success)
                {
                    if (result.AddedCount > 0)
                    {
                        for (var i = 0; i < result.AddedCount; i++)
                        {
                            await _usageChargeService.ChargeUsageIncreaseAsync(
                                companyNetworkId,
                                user!.Id,
                                PricingChargeUnit.PerSubscriber);
                        }
                    }
                    TempData["Success"] = $"✅ {result.Message}";
                    if (result.FailedCount > 0 && result.Errors.Any())
                    {
                        TempData["ImportWarnings"] = string.Join(" | ", result.Errors.Take(5));
                    }
                    if (result.UsersFailedCount > 0 && result.Errors.Any())
                    {
                        var failedUserDetails = result.Errors
                            .Where(e => !string.IsNullOrWhiteSpace(e))
                            .Take(15)
                            .ToList();
                        TempData["ImportFailedUsersDetails"] = System.Text.Json.JsonSerializer.Serialize(failedUserDetails);
                    }
                }
                else
                {
                    TempData["Error"] = $"❌ {result.Message}";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في استيراد المشتركين من السيرفر {ServerId}", serverId);
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في الاستيراد", ex.Message)}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: Clients/ExpiredAccounts
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        public async Task<IActionResult> ExpiredAccounts()
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canView = await _permissionService.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var today = DateTime.Now.Date;

            // جلب الحسابات المنتهية الصلاحية للشبكة المحددة فقط
            var expiredAccounts = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value
                    && c.AccountExpirationDate.HasValue 
                    && c.AccountExpirationDate.Value.Date < today)
                .Include(c => c.Profile)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Receiver)
                .OrderBy(c => c.AccountExpirationDate)
                .ToListAsync();

            // حساب الإحصائيات
            ViewBag.TotalExpired = expiredAccounts.Count;
            ViewBag.ActiveExpired = expiredAccounts.Count(c => c.IsActive);
            ViewBag.DisabledExpired = expiredAccounts.Count(c => !c.IsActive);

            return View(expiredAccounts);
        }

        // GET: Clients/ExpiringIn3Days
        [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,Employee")]
        public async Task<IActionResult> ExpiringIn3Days()
        {
            if (User.IsInRole(RoleNames.CompanyEmployee) || User.IsInRole(RoleNames.EmployeeLegacy))
            {
                var canView = await _permissionService.HasPermissionAsync(User, "Clients.View");
                if (!canView)
                {
                    return Forbid();
                }
            }

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var today = DateTime.Now.Date;
            var in3Days = today.AddDays(3);

            // جلب الحسابات التي ستنتهي خلال 3 أيام للشبكة المحددة فقط
            var expiringAccounts = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value
                    && c.AccountExpirationDate.HasValue 
                    && c.AccountExpirationDate.Value.Date >= today
                    && c.AccountExpirationDate.Value.Date <= in3Days
                    && c.IsActive)
                .Include(c => c.Profile)
                .Include(c => c.MikroTikServer)
                .Include(c => c.Receiver)
                .OrderBy(c => c.AccountExpirationDate)
                .ToListAsync();

            // حساب الإحصائيات
            ViewBag.TotalExpiring = expiringAccounts.Count;
            ViewBag.ExpiringToday = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today);
            ViewBag.ExpiringTomorrow = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today.AddDays(1));
            ViewBag.ExpiringIn2Days = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today.AddDays(2));
            ViewBag.ExpiringIn3Days = expiringAccounts.Count(c => c.AccountExpirationDate!.Value.Date == today.AddDays(3));

            return View(expiringAccounts);
        }

        // POST: Clients/QuickExtend - تمديد سريع لعدد أيام محدد
        [HttpPost]
        [Authorize(Roles = "NetworkAdministrator")]
        public async Task<IActionResult> QuickExtend(int id, int days)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var client = await _context.Clients
                .Where(c => c.NetworkId == networkId.Value)
                .Include(c => c.MikroTikServer)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (client == null)
            {
                return NotFound();
            }

            try
            {
                // حساب تاريخ الانتهاء الجديد
                DateTime newExpirationDate;
                if (client.AccountExpirationDate.HasValue && client.AccountExpirationDate.Value > DateTime.Now)
                {
                    // إذا كان الاشتراك لم ينته بعد، نضيف الأيام من تاريخ الانتهاء الحالي
                    newExpirationDate = client.AccountExpirationDate.Value.AddDays(days);
                }
                else
                {
                    // إذا كان الاشتراك منتهي، نضيف الأيام من اليوم
                    newExpirationDate = DateTime.Now.AddDays(days);
                }

                if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
                {
                    // تحديث في المايكروتك
                    await _mikroTikService.RenewPPPoESubscription(
                        client.UserName,
                        client.MikroTikServerId.Value,
                        newExpirationDate);
                }

                // تحديث في قاعدة البيانات
                client.AccountExpirationDate = newExpirationDate;
                client.LastRenewalDate = DateTime.Now.Date;
                client.IsActive = true;
                client.LastUpdated = DateTime.Now;
                _context.Update(client);
                await _context.SaveChangesAsync();

                TempData["Success"] = $"✅ تم تمديد اشتراك {client.Name} لمدة {days} أيام حتى {newExpirationDate:yyyy/MM/dd}";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ {BuildFriendlyMikroTikErrorMessage("خطأ في التمديد", ex.Message)}";
                _logger.LogError(ex, "خطأ في تمديد الاشتراك للعميل {UserName}", client.UserName);
            }

            // العودة للصفحة السابقة
            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer))
            {
                return Redirect(referer);
            }
            return RedirectToAction(nameof(Index));
        }

        // AJAX: جلب البروفايلات حسب الخادم من قاعدة البيانات
        public async Task<IActionResult> GetProfilesByServer(int serverId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new List<object>());
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    return Json(new List<object>());
                }

                var profiles = await _context.Profiles
                    .Where(p => p.MikroTikServerId == serverId && p.IsActive && p.NetworkId == networkId.Value)
                    .OrderBy(p => p.DisplayOrder)
                    .ThenBy(p => p.Name)
                    .Select(p => new { id = p.Id, name = p.Name })
                    .ToListAsync();
                return Json(profiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب البروفايلات للخادم {ServerId}", serverId);
                return Json(new List<object>());
            }
        }

        // AJAX: جلب المستقبلات المرتبطة بمرسلات الخادم المحدد
        public async Task<IActionResult> GetReceiversByServer(int serverId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new List<object>());
                }

                var server = await _context.MikroTikServers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    return Json(new List<object>());
                }

                var receivers = await _context.Receivers
                    .AsNoTracking()
                    .Where(r => r.NetworkId == networkId.Value && r.Sector.MikroTikServerId == serverId)
                    .OrderBy(r => r.Name)
                    .Select(r => new
                    {
                        id = r.Id,
                        name = r.Name,
                        sectorName = r.Sector.Name
                    })
                    .ToListAsync();

                return Json(receivers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب المستقبلات للخادم {ServerId}", serverId);
                return Json(new List<object>());
            }
        }

        // دوال مساعدة
        private bool ValidateClientData(Client client)
        {
            if (string.IsNullOrWhiteSpace(client.Name))
            {
                ModelState.AddModelError("Name", "الاسم مطلوب");
                return false;
            }

            if (string.IsNullOrWhiteSpace(client.UserName))
            {
                ModelState.AddModelError("UserName", "اسم المستخدم مطلوب");
                return false;
            }

            if (string.IsNullOrWhiteSpace(client.Password))
            {
                ModelState.AddModelError("Password", "كلمة المرور مطلوبة");
                return false;
            }

            if (client.ProfileId <= 0)
            {
                ModelState.AddModelError("ProfileId", "البروفايل مطلوب");
                return false;
            }

            if (!client.MikroTikServerId.HasValue)
            {
                ModelState.AddModelError("MikroTikServerId", "يجب اختيار خادم المايكروتك");
                return false;
            }

            return true;
        }

        // دالة لتنظيف البيانات في حالة الفشل
        private async Task CleanupFailedCreation(Client client)
        {
            if (client.MikroTikServerId.HasValue && !string.IsNullOrEmpty(client.UserName))
            {
                try
                {
                    await _mikroTikService.DeletePPPoEUser(client.UserName, client.MikroTikServerId.Value);
                    _logger.LogInformation($"✅ تم تنظيف المستخدم {client.UserName} من المايكروتك بعد فشل العملية");
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogError(cleanupEx, "⚠️ فشل في تنظيف المستخدم {UserName} من المايكروتك", client.UserName);
                }
            }
        }

        private async Task CreateEmployeeApprovalRequestAsync(
            int selectedNetworkId,
            string actorUserId,
            string featureKey,
            string notes,
            decimal expectedChargeAmountSyp = 0m)
        {
            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            var request = new NetworkServiceRequest
            {
                NetworkId = companyNetworkId,
                FeatureKey = featureKey,
                BillingPeriod = PricingBillingPeriod.OneTime,
                AmountSYP = Math.Max(0m, WalletMath.CeilSyp(expectedChargeAmountSyp)),
                AmountUSD = 0m,
                Currency = PricingCurrency.SYP_New,
                Status = NetworkServiceRequestStatus.Pending,
                RequestedByUserId = actorUserId,
                RequestedAt = DateTime.Now,
                Notes = notes
            };
            _context.NetworkServiceRequests.Add(request);

            await _context.SaveChangesAsync();
            await CreateManagerApprovalNotificationsAsync(companyNetworkId, featureKey, request.Id);
        }

        private async Task CreateManagerApprovalNotificationsAsync(int companyNetworkId, string featureKey, int requestId)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

            var managerUserId = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId)
                .Select(n => n.ManagerUserId)
                .FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(managerUserId))
            {
                recipients.Add(managerUserId);
            }

            var roleUserIds = await _context.Users
                .AsNoTracking()
                .Where(u => u.NetworkId.HasValue && companyScope.Contains(u.NetworkId.Value))
                .Join(_context.UserRoles.AsNoTracking(), u => u.Id, ur => ur.UserId, (u, ur) => new { u.Id, ur.RoleId })
                .Join(_context.Roles.AsNoTracking().Where(r => r.Name == RoleNames.NetworkAdministrator),
                    x => x.RoleId,
                    r => r.Id,
                    (x, _) => x.Id)
                .Distinct()
                .ToListAsync();
            foreach (var uid in roleUserIds)
            {
                recipients.Add(uid);
            }

            if (recipients.Count == 0)
            {
                return;
            }

            var actionLabel = featureKey == FeatureKeys.Clients ? "العميل" : "الخدمة";
            var now = DateTime.Now;
            var rows = recipients.Select(uid => new UserNotification
            {
                Key = $"EmployeeApprovalPending:{featureKey}:{requestId}:{uid}:{Guid.NewGuid():N}",
                UserId = uid,
                NetworkId = companyNetworkId,
                Type = NotificationType.SubscriptionExpiring,
                Title = "طلب موافقة جديد من موظف",
                Message = $"يوجد طلب {actionLabel} من موظف بانتظار اعتمادك.",
                CreatedAt = now,
                IsRead = false
            });

            _context.UserNotifications.AddRange(rows);
            await _context.SaveChangesAsync();
        }

        private async Task<HashSet<int>> GetPendingClientIdsAsync(int selectedNetworkId)
        {
            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

            var notes = await _context.NetworkServiceRequests
                .AsNoTracking()
                .Where(r =>
                    r.NetworkId == companyNetworkId &&
                    r.Status == NetworkServiceRequestStatus.Pending &&
                    r.FeatureKey == FeatureKeys.Clients &&
                    r.Notes != null &&
                    r.Notes.StartsWith("EMP_REQ:CLIENT_"))
                .Select(r => r.Notes!)
                .ToListAsync();

            var ids = new HashSet<int>();
            foreach (var note in notes)
            {
                if (EmployeeApprovalRequestHelper.TryParse(note, out var kind, out var entityId, out _) &&
                    (kind == EmployeeApprovalRequestKind.ClientCreate || kind == EmployeeApprovalRequestKind.ClientEdit))
                {
                    ids.Add(entityId);
                }
            }

            return ids;
        }

        private async Task<bool> IsPendingClientApprovalAsync(Client client)
        {
            if (!client.NetworkId.HasValue)
            {
                return false;
            }

            var pendingIds = await GetPendingClientIdsAsync(client.NetworkId.Value);
            if (pendingIds.Contains(client.Id))
            {
                return true;
            }

            return string.Equals(client.ConnectionStatus, "معلق بانتظار موافقة مدير الشركة", StringComparison.OrdinalIgnoreCase);
        }

        private async Task PrepareViewDataForCreate(Client client)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return;
            }

            // تصفية جميع القوائم حسب الشبكة
            var receiversQuery = _context.Receivers
                .Where(r => r.NetworkId == networkId.Value);
            if (client.MikroTikServerId.HasValue)
            {
                receiversQuery = receiversQuery.Where(r => r.Sector.MikroTikServerId == client.MikroTikServerId.Value);
            }

            var receivers = await receiversQuery
                .Include(r => r.Sector)
                .OrderBy(r => r.Name)
                .ToListAsync();
            var servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            var profiles = await _context.Profiles
                .Where(p => p.IsActive && p.NetworkId == networkId.Value)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Name)
                .ToListAsync();

            ViewData["ReceiverId"] = new SelectList(receivers, "Id", "Name", client.ReceiverId);
            ViewData["MikroTikServerId"] = new SelectList(servers, "Id", "Name", client.MikroTikServerId);
            ViewData["ProfileId"] = new SelectList(profiles, "Id", "Name", client.ProfileId);

            await LoadClientCreatePricingNoteAsync(networkId.Value);
        }

        private async Task LoadClientCreatePricingNoteAsync(int selectedNetworkId)
        {
            try
            {
                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

                var subscriberEstimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSubscriber,
                    1);
                var clientPricingRows = await _context.FeaturePricings
                    .AsNoTracking()
                    .Where(p =>
                        p.IsActive &&
                        p.FeatureKey == FeatureKeys.Clients &&
                        p.ChargeUnit == PricingChargeUnit.PerSubscriber)
                    .OrderByDescending(p => p.UpdatedAt)
                    .ThenByDescending(p => p.Id)
                    .ToListAsync();

                var initialPricing = clientPricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
                var renewalPricing = clientPricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);

                ViewBag.ClientCreateChargeHasPricing = subscriberEstimate.HasCharge;
                ViewBag.ClientCreateChargeAmount = subscriberEstimate.RequiredAmountSyp;
                ViewBag.ClientCreateSubscriberChargeAmount = subscriberEstimate.RequiredAmountSyp;
                ViewBag.ClientCreateUserChargeAmount = 0m;
                ViewBag.ClientCreateChargeWalletBalance = subscriberEstimate.WalletBalance > 0m
                    ? subscriberEstimate.WalletBalance
                    : 0m;
                ViewBag.ClientCreateInitialPrice = initialPricing?.AmountSYP ?? subscriberEstimate.RequiredAmountSyp;
                ViewBag.ClientCreateRenewalPrice = renewalPricing?.AmountSYP ?? 0m;
                ViewBag.ClientCreateRenewalPeriodLabel = renewalPricing != null
                    ? PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod)
                    : null;
                ViewBag.ClientCreateHasRenewalPricing = renewalPricing != null;
            }
            catch
            {
                ViewBag.ClientCreateChargeHasPricing = false;
                ViewBag.ClientCreateChargeAmount = 0m;
                ViewBag.ClientCreateSubscriberChargeAmount = 0m;
                ViewBag.ClientCreateUserChargeAmount = 0m;
                ViewBag.ClientCreateChargeWalletBalance = 0m;
                ViewBag.ClientCreateInitialPrice = 0m;
                ViewBag.ClientCreateRenewalPrice = 0m;
                ViewBag.ClientCreateRenewalPeriodLabel = null;
                ViewBag.ClientCreateHasRenewalPricing = false;
            }
        }

        private async Task<decimal> ResolveExpectedClientCreateChargeAsync(int selectedNetworkId)
        {
            try
            {
                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

                var estimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSubscriber,
                    1);
                return estimate.RequiredAmountSyp;
            }
            catch
            {
                return 0m;
            }
        }

        private async Task PrepareViewDataForEdit(Client client)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return;
            }

            // تصفية جميع القوائم حسب الشبكة
            var receivers = await _context.Receivers
                .Where(r => r.NetworkId == networkId.Value)
                .ToListAsync();
            var servers = await _context.MikroTikServers
                .Where(s => s.NetworkId == networkId.Value)
                .ToListAsync();
            var profiles = await _context.Profiles
                .Where(p => p.IsActive && p.NetworkId == networkId.Value)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Name)
                .ToListAsync();
            
            ViewData["ReceiverId"] = new SelectList(receivers, "Id", "Name", client.ReceiverId);
            ViewData["MikroTikServerId"] = new SelectList(servers, "Id", "Name", client.MikroTikServerId);
            ViewData["ProfileId"] = new SelectList(profiles, "Id", "Name", client.ProfileId);
        }

        private async Task<ContractMetaSettings> GetContractMetaAsync(int networkId)
        {
            var item = await _context.CustomServiceItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NetworkId == networkId && x.ServiceKey == ContractMetaServiceKey);

            if (item == null || string.IsNullOrWhiteSpace(item.Body))
            {
                return new ContractMetaSettings();
            }

            try
            {
                return JsonSerializer.Deserialize<ContractMetaSettings>(item.Body) ?? new ContractMetaSettings();
            }
            catch
            {
                return new ContractMetaSettings();
            }
        }

        private async Task<string> GetContractTemplateBodyAsync(int networkId)
        {
            var item = await _context.CustomServiceItems
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.NetworkId == networkId && x.ServiceKey == ContractTemplateServiceKey);

            if (item == null || string.IsNullOrWhiteSpace(item.Body))
            {
                return DefaultContractBodyHtml;
            }

            return item.Body;
        }

        private async Task UpsertCustomServiceItemAsync(int networkId, string serviceKey, string title, string body)
        {
            var existing = await _context.CustomServiceItems
                .FirstOrDefaultAsync(x => x.NetworkId == networkId && x.ServiceKey == serviceKey);

            if (existing == null)
            {
                _context.CustomServiceItems.Add(new CustomServiceItem
                {
                    NetworkId = networkId,
                    ServiceKey = serviceKey,
                    Title = title,
                    Body = body,
                    CreatedAt = DateTime.Now,
                    UpdatedAt = DateTime.Now
                });
            }
            else
            {
                existing.Title = title;
                existing.Body = body;
                existing.UpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
        }

        private static Dictionary<string, string> GetContractVariableMap()
        {
            return new Dictionary<string, string>
            {
                ["{{SubscriberName}}"] = "اسم المشترك",
                ["{{SubscriberNumber}}"] = "رقم المشترك (SID)",
                ["{{ContractDate}}"] = "تاريخ تحرير العقد",
                ["{{SubscriptionStartDate}}"] = "تاريخ الاشتراك",
                ["{{SubscriptionEndDate}}"] = "تاريخ انتهاء الاشتراك",
                ["{{ProfileName}}"] = "اسم البروفايل",
                ["{{NetworkName}}"] = "اسم الشبكة",
                ["{{ClientUserName}}"] = "اسم مستخدم المشترك"
            };
        }

        private static string RenderContractTemplate(string template, Client client, DateTime contractDate)
        {
            var profileName = client.Profile?.Name ?? client.ProfileName ?? "-";
            var networkName = client.Network?.Name ?? "-";

            var replacements = new Dictionary<string, string>
            {
                ["{{SubscriberName}}"] = client.Name ?? "-",
                ["{{SubscriberNumber}}"] = client.SID ?? "-",
                ["{{ContractDate}}"] = contractDate.ToString("yyyy/MM/dd"),
                ["{{SubscriptionStartDate}}"] = client.ServiceStartDate?.ToString("yyyy/MM/dd") ?? "-",
                ["{{SubscriptionEndDate}}"] = client.AccountExpirationDate?.ToString("yyyy/MM/dd") ?? "-",
                ["{{ProfileName}}"] = profileName,
                ["{{NetworkName}}"] = networkName,
                ["{{ClientUserName}}"] = client.UserName ?? "-"
            };

            var rendered = template ?? string.Empty;
            foreach (var pair in replacements)
            {
                rendered = rendered.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
            }

            return rendered;
        }

        private static List<string> FindUnknownTemplateVariables(string? template, IEnumerable<string> allowedVariables)
        {
            if (string.IsNullOrWhiteSpace(template))
            {
                return new List<string>();
            }

            var allowed = new HashSet<string>(allowedVariables, StringComparer.Ordinal);
            var found = Regex.Matches(template, @"\{\{[^{}]+\}\}")
                .Select(m => m.Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();

            return found.Where(v => !allowed.Contains(v)).ToList();
        }

        private bool ClientExists(int id)
        {
            return _context.Clients.Any(e => e.Id == id);
        }

        private static string BuildFriendlyMikroTikErrorMessage(string prefix, string? rawMessage)
        {
            var message = (rawMessage ?? string.Empty).Trim();
            if (message.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("transport connection", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("Unable to read data from the transport connection", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
                message.Contains("socket", StringComparison.OrdinalIgnoreCase))
            {
                return
                    $"{prefix}: تعذر الاتصال بخادم MikroTik لأن الاتصال انقطع. " +
                    "تحقق من صحة Host/Port وتفعيل API أو API-SSL والسماح بالاتصال عبر الجدار الناري.";
            }

            return string.IsNullOrWhiteSpace(message) ? prefix : $"{prefix}: {message}";
        }

        private class ContractMetaSettings
        {
            public string? ContractTitle { get; set; }
            public string? RecordNumber { get; set; }
            public string? LicenseNumber { get; set; }
        }
    }
}