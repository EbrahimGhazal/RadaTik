using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Helpers;
using RadaTik.Constants;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.PricingPreview;
using RadaTik.Services.SystemAdminPricing;
using RadaTik.ViewModels.Network;
using System;
using Microsoft.EntityFrameworkCore.Storage;

namespace RadaTik.Controllers
{
    [Authorize(Roles = "SystemAdministrator,NetworkAdministrator")]
    public class NetworkController : Controller
    {
        private const string NetworksFeatureKey = FeatureKeys.Networks;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<NetworkController> _logger;
        private readonly IWebHostEnvironment _environment;
        private readonly IUsageBasedSubscriptionChargeService _usageSubscriptionChargeService;
        private readonly ICreatePricingPreviewService _pricingPreviewService;
        private readonly ICompanyWalletOnboardingFundingService _fundingService;

        public NetworkController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<NetworkController> logger,
            IWebHostEnvironment environment,
            IUsageBasedSubscriptionChargeService usageSubscriptionChargeService,
            ICreatePricingPreviewService pricingPreviewService,
            ICompanyWalletOnboardingFundingService fundingService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
            _usageSubscriptionChargeService = usageSubscriptionChargeService;
            _pricingPreviewService = pricingPreviewService;
            _fundingService = fundingService;
        }

        // GET: Network
        // مدير النظام: عرض الشبكات الرئيسية مع مدير الشركة والرصيد. مدير الشركة: عرض شبكته.
        public async Task<IActionResult> Index(string? status = null, string? q = null, string? scope = null)
        {
            bool isSysAdmin = User.IsInRole("SystemAdministrator");
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            const string ScopeSessionKey = "NetworkIndexScopeFilter";

            // Remember last selected scope filter in session (all/main/sub).
            if (!string.IsNullOrWhiteSpace(scope))
            {
                string normalizedScope = scope.Trim().ToLowerInvariant();
                if (normalizedScope is "all" or "main" or "sub")
                {
                    HttpContext.Session.SetString(ScopeSessionKey, normalizedScope);
                    scope = normalizedScope;
                }
                else
                {
                    scope = "all";
                }
            }
            else
            {
                scope = HttpContext.Session.GetString(ScopeSessionKey) ?? "all";
            }

            IQueryable<Network> query = _context.Networks
                .Where(n => n.ParentNetworkId == null)
                .Include(n => n.ChildNetworks)
                .Include(n => n.Clients)
                .Include(n => n.ManagerUser)
                .Include(n => n.Users);

            if (!isSysAdmin && user?.NetworkId.HasValue == true)
            {
                query = query.Where(n => n.Id == user.NetworkId.Value);
            }

            List<Network> all = await query.OrderBy(n => n.Name).ToListAsync();

            HashSet<string> naIds = new HashSet<string>();
            if (isSysAdmin)
            {
                IList<ApplicationUser> nas = await _userManager.GetUsersInRoleAsync("NetworkAdministrator");
                foreach (ApplicationUser u in nas)
                {
                    naIds.Add(u.Id);
                }
            }

            Dictionary<int, string> managerNames = new Dictionary<int, string>();
            foreach (Network? n in all)
            {
                string name = "-";
                if (n.ManagerUserId != null && n.ManagerUser != null)
                {
                    name = n.ManagerUser.FullName ?? n.ManagerUser.UserName ?? "-";
                }
                else if (isSysAdmin && n.Users != null)
                {
                    ApplicationUser? mgr = n.Users.FirstOrDefault(u => naIds.Contains(u.Id));
                    if (mgr != null)
                    {
                        name = mgr.FullName ?? mgr.UserName ?? "-";
                    }
                }
                managerNames[n.Id] = name;
            }

            ViewBag.TotalCompanies = all.Count;
            ViewBag.ActiveCompanies = all.Count(c => c.Status == NetworkStatus.Active);
            ViewBag.InactiveCompanies = all.Count(c => c.Status == NetworkStatus.Inactive);
            ViewBag.UnderConstructionCompanies = all.Count(c => c.Status == NetworkStatus.UnderConstruction);
            ViewBag.StatusFilter = string.IsNullOrEmpty(status) || status.Equals("All", StringComparison.OrdinalIgnoreCase) ? null : status;
            ViewBag.ManagerNames = managerNames;
            ViewBag.IsSystemAdmin = isSysAdmin;
            ViewBag.Query = q;
            ViewBag.ScopeFilter = scope;

            List<Network> companies = all;
            if (!string.IsNullOrEmpty(ViewBag.StatusFilter))
            {
                string sf = (string)ViewBag.StatusFilter;
                if (Enum.TryParse<NetworkStatus>(sf, ignoreCase: true, out NetworkStatus st))
                {
                    companies = companies.Where(c => c.Status == st).ToList();
                }
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                string term = q.Trim();
                companies = companies
                    .Where(c =>
                        (c.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                        (managerNames.TryGetValue(c.Id, out string? mgr) && mgr.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return View(companies);
        }

        // GET: Network/Create — مدير النظام لا يمكنه إضافة شبكات
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("SystemAdministrator"))
            {
                return Forbid();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            await PopulateNetworkPricingPreviewAsync(user);
            return View();
        }

        // POST: Network/Create — مدير النظام لا يمكنه إضافة شبكات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NetworkViewModel model, IFormFile? logoFile)
        {
            if (User.IsInRole("SystemAdministrator"))
            {
                return Forbid();
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
            {
                return RedirectToAction("Login", "Account");
            }

            if (ImageUploadRules.IsTooLarge(logoFile))
            {
                ModelState.AddModelError(nameof(model.LogoFile), ImageUploadRules.MaxNetworkLogoSizeMessage);
            }

            (FeaturePricing? initialNetworkPricing, FeaturePricing? renewalNetworkPricing) = await GetNetworkPricingSettingsAsync();

            if (!IsNetworkPricingConfigured(initialNetworkPricing, renewalNetworkPricing))
            {
                ModelState.AddModelError(string.Empty, AppMessages.NetworkPricingNotConfigured);
            }

            if (ModelState.IsValid)
            {
                bool isMain = !currentUser.NetworkId.HasValue;
                Network network = new Network
                {
                    Name = model.Name,
                    Governorates = model.Governorates,
                    CreationDate = DateTime.Now,
                    Status = model.Status,
                    Notes = model.Notes,
                    Balance = 0m,
                    ParentNetworkId = currentUser.NetworkId.HasValue ? currentUser.NetworkId.Value : null
                };
                if (isMain)
                {
                    network.ManagerUserId = currentUser.Id;
                }

                // رفع الشعار إذا تم تحميله
                if (logoFile != null && logoFile.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "networks");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + logoFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(fileStream);
                    }

                    network.LogoPath = $"/uploads/networks/{uniqueFileName}";
                }

                RecurringPricingPolicy recurringPolicy = RecurringPricingPolicyCodec.ReadFromPricings(initialNetworkPricing!, renewalNetworkPricing!);
                int companyNetworkIdForBilling = currentUser.NetworkId ?? 0;
                Network? companyNetworkForBilling = default(Network);
                decimal oneTimeChargeAmount = 0m;
                int companyNetworksBeforeCreation = 0;

                if (!isMain)
                {
                    companyNetworkForBilling = await _context.Networks
                        .FirstOrDefaultAsync(n => n.Id == companyNetworkIdForBilling && n.ParentNetworkId == null);

                    if (companyNetworkForBilling == null)
                    {
                        ModelState.AddModelError(string.Empty, "تعذر تحديد حساب الشركة الرئيسي لعملية خصم إنشاء الشبكة.");
                        await PopulateNetworkPricingPreviewAsync(currentUser);
                        return View(model);
                    }

                    companyNetworksBeforeCreation = await _context.Networks
                        .CountAsync(n => n.Id == companyNetworkIdForBilling || n.ParentNetworkId == companyNetworkIdForBilling);
                }
                else
                {
                    companyNetworksBeforeCreation = 0;
                }

                bool shouldChargeNow = companyNetworksBeforeCreation >= recurringPolicy.FreeInitialUnits;
                if (shouldChargeNow)
                {
                    oneTimeChargeAmount = WalletMath.CeilSyp(initialNetworkPricing!.AmountSYP);
                    if (oneTimeChargeAmount < 0)
                    {
                        oneTimeChargeAmount = 0m;
                    }

                    if (!isMain && oneTimeChargeAmount > 0 && companyNetworkForBilling != null && companyNetworkForBilling.Balance < oneTimeChargeAmount)
                    {
                        TempData["WalletTopUpRequired"] = "1";
                        ModelState.AddModelError(
                            string.Empty,
                            $"{AppMessages.InsufficientBalance} يمكنك طلب تغذية الرصيد من صفحة المحفظة قبل إنشاء شبكة إضافية مدفوعة.");
                        await PopulateNetworkPricingPreviewAsync(currentUser);
                        return View(model);
                    }
                }

                try
                {
                    await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();

                    // التحقق من عدم وجود شبكة بنفس الاسم
                    Network? existingNetwork = await _context.Networks
                        .FirstOrDefaultAsync(n => n.Name == network.Name);

                    if (existingNetwork != null)
                    {
                        ModelState.AddModelError("Name", "يوجد بالفعل شبكة بهذا الاسم. يرجى اختيار اسم آخر.");
                        await PopulateNetworkPricingPreviewAsync(currentUser);
                        return View(model);
                    }

                    _context.Networks.Add(network);
                    await _context.SaveChangesAsync();

                    // ربط المستخدم بالشبكة الرئيسية (فقط إذا لم يكن لديه شبكة)
                    // ملاحظة: شبكة المستخدم الأساسية تمثل "الشبكة/الشركة الرئيسية"
                    if (!currentUser.NetworkId.HasValue)
                    {
                        currentUser.NetworkId = network.Id;
                        await _userManager.UpdateAsync(currentUser);
                    }

                    // تعيين الشبكة الجديدة في Session (للبدء بالعمل عليها مباشرة)
                    NetworkHelper.SetCurrentNetworkId(HttpContext, network.Id);

                    DateTime now = DateTime.Now;
                    int companyNetworkId = isMain ? network.Id : companyNetworkIdForBilling;

                    if (renewalNetworkPricing != null && renewalNetworkPricing.BillingPeriod != PricingBillingPeriod.OneTime)
                    {
                        try
                        {
                            await EnsureNetworksSubscriptionAsync(
                                companyNetworkId,
                                renewalNetworkPricing.BillingPeriod,
                                now,
                                HttpContext.RequestAborted);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "تعذر تهيئة اشتراك خدمة الشبكات للشركة #{NetworkId}",
                                companyNetworkId);
                        }
                    }

                    if (isMain)
                    {
                        try
                        {
                            await CompanySubscriptionBootstrap.SeedActiveSubscriptionsForNewMainCompanyNetworkAsync(
                                _context,
                                _usageSubscriptionChargeService,
                                network.Id,
                                _logger,
                                HttpContext.RequestAborted);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "تعذر تهيئة اشتراكات الخدمات الافتراضية للشبكة الرئيسية #{NetworkId}",
                                network.Id);
                        }

                        if (shouldChargeNow && oneTimeChargeAmount > 0)
                        {
                            bool charged = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
                                _context,
                                network.Id,
                                network.Name,
                                NetworksFeatureKey,
                                oneTimeChargeAmount,
                                currentUser.Id,
                                HttpContext.RequestAborted);
                            if (!charged)
                            {
                                TempData["PendingMainNetworkCreationChargeSyp"] = oneTimeChargeAmount.ToString(System.Globalization.CultureInfo.InvariantCulture);
                            }
                        }
                    }
                    else if (companyNetworkForBilling != null && oneTimeChargeAmount > 0)
                    {
                        decimal previousBalance = companyNetworkForBilling.Balance;
                        companyNetworkForBilling.Balance -= oneTimeChargeAmount;

                        _context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                        {
                            NetworkId = companyNetworkId,
                            Type = NetworkWalletTransactionType.ServiceCharge,
                            SignedAmount = -oneTimeChargeAmount,
                            PreviousBalance = previousBalance,
                            NewBalance = companyNetworkForBilling.Balance,
                            CreatedByUserId = currentUser.Id,
                            CreatedAt = now,
                            Notes = $"إنشاء شبكة إضافية: {network.Name} ({NetworksFeatureKey} / {PricingBillingPeriod.OneTime} / {PricingChargeUnit.PerNetwork})"
                        });
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    if (isMain)
                    {
                        TempData["WalletOnboardingStep"] = "1";
                        decimal minRequired = await _fundingService.GetRequiredMinimumSypAsync(HttpContext.RequestAborted);
                        if (minRequired > 0m)
                        {
                            TempData["Success"] =
                                $"تم إنشاء شبكتك بنجاح. الخطوة التالية: طلب تغذية الرصيد بمبلغ لا يقل عن {SyrianCurrencyHelper.FormatNew(minRequired)} ل.س.ج (سعر إنشاء الشركة).";
                        }
                        else
                        {
                            TempData["Success"] =
                                "تم إنشاء شبكتك بنجاح. الخطوة التالية: طلب تغذية الرصيد لاستخدام الخدمات المدفوعة عند الحاجة.";
                        }

                        return RedirectToRoute("networkManager-wallet-topup");
                    }

                    TempData["Success"] = AppMessages.OperationSuccess;
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "خطأ في حفظ بيانات الشبكة");

                    // التحقق من خطأ التكرار
                    if (ex.InnerException != null && ex.InnerException.Message.Contains("IX_Networks_Name"))
                    {
                        ModelState.AddModelError("Name", "يوجد بالفعل شبكة بهذا الاسم. يرجى اختيار اسم آخر.");
                    }
                    else
                    {
                        ModelState.AddModelError(string.Empty, AppMessages.SaveFailed);
                    }

                    await PopulateNetworkPricingPreviewAsync(currentUser);
                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ غير متوقع في إنشاء الشبكة");
                    ModelState.AddModelError(string.Empty, AppMessages.UnexpectedError);
                    await PopulateNetworkPricingPreviewAsync(currentUser);
                    return View(model);
                }
            }

            await PopulateNetworkPricingPreviewAsync(currentUser);
            return View(model);
        }

        // GET: Network/Edit/5 — مدير النظام لا يمكنه تعديل بيانات الشبكة
        public async Task<IActionResult> Edit(int? id)
        {
            if (User.IsInRole("SystemAdministrator"))
            {
                return Forbid();
            }

            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, id.Value))
            {
                return Forbid();
            }

            Network? network = await _context.Networks.FindAsync(id);
            if (network == null)
            {
                return NotFound();
            }

            NetworkViewModel model = new NetworkViewModel
            {
                Id = network.Id,
                Name = network.Name,
                Governorates = network.Governorates,
                LogoPath = network.LogoPath,
                Status = network.Status,
                Notes = network.Notes,
                IsMainCompanyNetwork = network.ParentNetworkId == null,
                DefaultUsdToSypExchangeRate = network.ParentNetworkId == null
                    ? network.DefaultUsdToSypExchangeRate
                    : null,
                DefaultMaterialInvoiceCurrency = network.ParentNetworkId == null
                    ? network.DefaultMaterialInvoiceCurrency
                    : PricingCurrency.SYP_New
            };

            if (network.ParentNetworkId.HasValue)
            {
                Network? parent = await _context.Networks.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == network.ParentNetworkId.Value);
                model.DefaultUsdToSypExchangeRate = parent?.DefaultUsdToSypExchangeRate;
                model.DefaultMaterialInvoiceCurrency = parent?.DefaultMaterialInvoiceCurrency ?? PricingCurrency.SYP_New;
            }

            return View(model);
        }

        // POST: Network/Edit/5 — مدير النظام لا يمكنه تعديل بيانات الشبكة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NetworkViewModel model, IFormFile? logoFile)
        {
            if (User.IsInRole("SystemAdministrator"))
            {
                return Forbid();
            }

            if (id != model.Id)
            {
                return NotFound();
            }

            if (ImageUploadRules.IsTooLarge(logoFile))
            {
                ModelState.AddModelError(nameof(model.LogoFile), ImageUploadRules.MaxNetworkLogoSizeMessage);
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, id))
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                Network? network = await _context.Networks.FindAsync(id);
                if (network == null)
                {
                    return NotFound();
                }

                network.Name = model.Name;
                network.Governorates = model.Governorates;
                network.Status = model.Status;
                network.Notes = model.Notes;

                if (network.ParentNetworkId == null)
                {
                    if (model.DefaultUsdToSypExchangeRate.HasValue && model.DefaultUsdToSypExchangeRate.Value > 0m)
                    {
                        network.DefaultUsdToSypExchangeRate = model.DefaultUsdToSypExchangeRate.Value;
                    }

                    network.DefaultMaterialInvoiceCurrency = model.DefaultMaterialInvoiceCurrency;
                }

                // تحديث الشعار إذا تم تحميل ملف جديد
                if (logoFile != null && logoFile.Length > 0)
                {
                    // حذف الشعار القديم إن وجد
                    if (!string.IsNullOrEmpty(network.LogoPath))
                    {
                        string oldFilePath = Path.Combine(_environment.WebRootPath, network.LogoPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // رفع الشعار الجديد
                    string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "networks");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + logoFile.FileName;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (FileStream fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(fileStream);
                    }

                    network.LogoPath = $"/uploads/networks/{uniqueFileName}";
                }

                try
                {
                    _context.Update(network);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = AppMessages.OperationSuccess;
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!NetworkExists(network.Id))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            return View(model);
        }

        // GET: Network/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null || !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, id.Value))
            {
                return Forbid();
            }

            Network? network = await _context.Networks
                .Include(n => n.MikroTikServers)
                .Include(n => n.Sectors)
                .Include(n => n.Receivers)
                .Include(n => n.Clients)
                .Include(n => n.Profiles)
                .FirstOrDefaultAsync(n => n.Id == id);

            if (network == null)
            {
                return NotFound();
            }

            // إحصائيات الشبكة
            ViewBag.TotalServers = network.MikroTikServers?.Count ?? 0;
            ViewBag.ActiveServers = network.MikroTikServers?.Count(s => s.IsActive) ?? 0;
            ViewBag.TotalSectors = network.Sectors?.Count ?? 0;
            ViewBag.ActiveSectors = network.Sectors?.Count(s => s.IsActive) ?? 0;
            ViewBag.TotalReceivers = network.Receivers?.Count ?? 0;
            ViewBag.ActiveReceivers = network.Receivers?.Count(r => r.IsActive) ?? 0;
            ViewBag.TotalClients = network.Clients?.Count ?? 0;
            ViewBag.ActiveClients = network.Clients?.Count(c => c.IsActive) ?? 0;
            ViewBag.TotalProfiles = network.Profiles?.Count ?? 0;
            ViewBag.ActiveProfiles = network.Profiles?.Count(p => p.IsActive) ?? 0;

            int mainId = network.ParentNetworkId ?? network.Id;
            ViewBag.ManagerName = await GetManagerNameForNetworkAsync(mainId);
            ViewBag.IsSystemAdmin = User.IsInRole("SystemAdministrator");
            ViewBag.MainBalance = network.ParentNetworkId.HasValue
                ? (await _context.Networks.FindAsync(network.ParentNetworkId))?.Balance ?? 0m
                : network.Balance;
            return View(network);
        }

        /// <summary>
        /// تعديل حالة حساب مدير الشركة — مدير النظام فقط. عند التعديل تُحدَّث كل الشبكات التابعة لنفس الحالة.
        /// </summary>
        [Authorize(Roles = "SystemAdministrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> EditManagerStatus(int networkId, bool isActive)
        {
            Network? main = await _context.Networks
                .Include(n => n.ManagerUser)
                .Include(n => n.Users)
                .FirstOrDefaultAsync(n => n.Id == networkId && n.ParentNetworkId == null);
            if (main == null)
            {
                return NotFound();
            }

            ApplicationUser? manager = main.ManagerUserId != null ? main.ManagerUser : null;
            if (manager == null && main.Users != null)
            {
                HashSet<string> naIds = (await _userManager.GetUsersInRoleAsync("NetworkAdministrator")).Select(u => u.Id).ToHashSet();
                manager = main.Users.FirstOrDefault(u => naIds.Contains(u.Id));
            }
            if (manager == null)
            {
                TempData["Error"] = "لم يتم العثور على مدير شركة لهذه الشبكة.";
                return RedirectToAction(nameof(Index));
            }

            manager.IsActive = isActive;
            await _userManager.UpdateAsync(manager);

            NetworkStatus status = isActive ? NetworkStatus.Active : NetworkStatus.Inactive;
            int mainId = main.Id;
            List<Network> toUpdate = await _context.Networks
                .Where(n => n.Id == mainId || n.ParentNetworkId == mainId)
                .ToListAsync();
            foreach (Network? n in toUpdate)
            {
                n.Status = status;
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = isActive
                ? "تم تفعيل حساب مدير الشركة وجميع الشبكات التابعة له."
                : "تم تعطيل حساب مدير الشركة وجميع الشبكات التابعة له.";
            return RedirectToAction(nameof(Index));
        }

        /// <summary>
        /// إضافة رصيد لمدير الشركة (الشبكة) — مدير النظام فقط.
        /// </summary>
        [Authorize(Roles = "SystemAdministrator")]
        [HttpGet]
        public async Task<IActionResult> AddBalance(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            Network? main = await _context.Networks
                .Include(n => n.ManagerUser)
                .FirstOrDefaultAsync(n => n.Id == id && n.ParentNetworkId == null);
            if (main == null)
            {
                return NotFound();
            }

            ViewBag.Network = main;
            ViewBag.ManagerName = main.ManagerUser?.FullName ?? main.ManagerUser?.UserName ?? "-";
            return View();
        }

        [Authorize(Roles = "SystemAdministrator")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddBalance(int networkId, decimal amount)
        {
            if (amount <= 0)
            {
                TempData["Error"] = "المبلغ يجب أن يكون موجباً.";
                return RedirectToAction(nameof(AddBalance), new { id = networkId });
            }
            Network? main = await _context.Networks
                .FirstOrDefaultAsync(n => n.Id == networkId && n.ParentNetworkId == null);
            if (main == null)
            {
                return NotFound();
            }

            main.Balance += amount;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم إضافة رصيد {amount:N2} بنجاح. الرصيد الحالي: {main.Balance:N2}.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GetManagerNameForNetworkAsync(int mainNetworkId)
        {
            Network? main = await _context.Networks
                .Include(n => n.ManagerUser)
                .Include(n => n.Users)
                .FirstOrDefaultAsync(n => n.Id == mainNetworkId && n.ParentNetworkId == null);
            if (main == null)
            {
                return "-";
            }

            if (main.ManagerUserId != null && main.ManagerUser != null)
            {
                return main.ManagerUser.FullName ?? main.ManagerUser.UserName ?? "-";
            }

            HashSet<string> naIds = (await _userManager.GetUsersInRoleAsync("NetworkAdministrator")).Select(u => u.Id).ToHashSet();
            ApplicationUser? mgr = main.Users?.FirstOrDefault(u => naIds.Contains(u.Id));
            return mgr != null ? (mgr.FullName ?? mgr.UserName ?? "-") : "-";
        }

        // GET: Network/SelectNetwork - إعادة توجيه عند الوصول بالرابط مباشرة (تفادي HTTP 405)
        [HttpGet]
        public IActionResult SelectNetwork()
        {
            if (User.IsInRole(RoleNames.NetworkAdministrator))
            {
                return RedirectToRoute("networkManager-dashboard");
            }

            if (User.IsInRole(RoleNames.SystemAdministrator))
            {
                return RedirectToRoute("systemAdmin-actions");
            }

            return RedirectToRoute("networkManager-dashboard");
        }

        // POST: Network/SelectNetwork
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SelectNetwork(int networkId)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // التحقق من أن المستخدم لديه صلاحية على هذه الشبكة
            bool allowed = await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, networkId);
            if (allowed)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, networkId);
                TempData["Success"] = AppMessages.OperationSuccess;
            }
            else
            {
                TempData["Error"] = "ليس لديك صلاحية على هذه الشبكة";
            }

            // إعادة التوجيه إلى لوحة التحكم المناسبة حسب دور المستخدم
            if (User.IsInRole(RoleNames.NetworkAdministrator))
            {
                return RedirectToRoute("networkManager-dashboard");
            }

            if (User.IsInRole(RoleNames.SystemAdministrator))
            {
                return RedirectToRoute("systemAdmin-actions");
            }

            return RedirectToRoute("networkManager-dashboard");
        }

        private bool NetworkExists(int id)
        {
            return _context.Networks.Any(e => e.Id == id);
        }

        private async Task<(FeaturePricing? Initial, FeaturePricing? Renewal)> GetNetworkPricingSettingsAsync()
        {
            List<FeaturePricing> pricingRows = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == NetworksFeatureKey &&
                    p.ChargeUnit == PricingChargeUnit.PerNetwork)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            FeaturePricing? initial = pricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
            FeaturePricing? renewal = pricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
            return (initial, renewal);
        }

        private async Task EnsureNetworksSubscriptionAsync(
            int companyNetworkId,
            PricingBillingPeriod renewalBillingPeriod,
            DateTime now,
            CancellationToken ct)
        {
            NetworkServiceSubscription? sub = await _context.NetworkServiceSubscriptions
                .FirstOrDefaultAsync(s => s.NetworkId == companyNetworkId && s.FeatureKey == NetworksFeatureKey, ct);

            if (sub == null)
            {
                sub = new NetworkServiceSubscription
                {
                    NetworkId = companyNetworkId,
                    FeatureKey = NetworksFeatureKey,
                    BillingPeriod = renewalBillingPeriod,
                    StartAt = now,
                    ExpiresAt = BillingPeriodDateCalculator.AddPeriod(now, renewalBillingPeriod),
                    Status = NetworkServiceSubscriptionStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.NetworkServiceSubscriptions.Add(sub);
                await _context.SaveChangesAsync(ct);
                await _usageSubscriptionChargeService.InitializeBaselineAsync(companyNetworkId, sub.Id, ct);
                return;
            }

            sub.BillingPeriod = renewalBillingPeriod;
            if (sub.ExpiresAt <= now)
            {
                sub.ExpiresAt = BillingPeriodDateCalculator.AddPeriod(now, renewalBillingPeriod);
            }
            sub.Status = NetworkServiceSubscriptionStatus.Active;
            sub.UpdatedAt = now;
            await _context.SaveChangesAsync(ct);
        }

        private async Task PopulateNetworkPricingPreviewAsync(ApplicationUser user)
        {
            (FeaturePricing? initialPricing, FeaturePricing? renewalPricing) = await GetNetworkPricingSettingsAsync();
            RecurringPricingPolicy recurringPolicy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, renewalPricing);

            bool hasInitialPricing = initialPricing != null;
            bool hasRenewalPricing = renewalPricing != null;
            int freeInitialUnits = recurringPolicy.FreeInitialUnits;
            int freeRenewalUnits = recurringPolicy.FreeRenewalUnits;
            decimal initialPriceSyp = initialPricing != null ? WalletMath.CeilSyp(initialPricing.AmountSYP) : 0m;
            decimal renewalPriceSyp = renewalPricing != null ? WalletMath.CeilSyp(renewalPricing.AmountSYP) : 0m;
            string renewalPeriodLabel = renewalPricing != null
                ? PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod)
                : "غير محدد";

            bool hasMainNetwork = user.NetworkId.HasValue;
            string companyNetworkName = "شبكة جديدة";
            int totalCompanyNetworks = 0;
            bool shouldChargeNow = 0 >= freeInitialUnits;

            if (hasMainNetwork)
            {
                Network? selected = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == user.NetworkId!.Value);

                if (selected != null)
                {
                    CreatePricingPreviewResult preview = await _pricingPreviewService.BuildAsync(
                        selected.Id,
                        FeatureKeys.Networks,
                        PricingChargeUnit.PerNetwork,
                        PricingPreviewCounterKeys.Networks);
                    PricingPreviewViewBagMapper.Apply(ViewData, "NetworkPricing", preview);
                    companyNetworkName = preview.CompanyName;
                    totalCompanyNetworks = preview.TotalUnits;
                    shouldChargeNow = preview.ShouldChargeNow;
                }
            }

            ViewBag.NetworkPricingIsFirstFree = !hasMainNetwork;
            ViewBag.NetworkPricingCompanyName = companyNetworkName;
            ViewBag.NetworkPricingTotalNetworks = totalCompanyNetworks;
            ViewBag.NetworkPricingHasInitial = hasInitialPricing;
            ViewBag.NetworkPricingHasRenewal = hasRenewalPricing;
            ViewBag.NetworkPricingFreeInitialUnits = freeInitialUnits;
            ViewBag.NetworkPricingFreeRenewalUnits = freeRenewalUnits;
            ViewBag.NetworkPricingInitialSyp = initialPriceSyp;
            ViewBag.NetworkPricingRenewalSyp = renewalPriceSyp;
            ViewBag.NetworkPricingRenewalPeriodLabel = renewalPeriodLabel;
            ViewBag.NetworkPricingShouldChargeNow = shouldChargeNow;
            ViewBag.NetworkPricingIsConfigured = hasInitialPricing && hasRenewalPricing;
            ViewBag.NetworkPricingMissingInitial = !hasInitialPricing;
            ViewBag.NetworkPricingMissingRenewal = !hasRenewalPricing;
            ViewBag.IsCompanyOnboarding = !hasMainNetwork;
            ViewBag.CanAccessWalletTopUp = hasMainNetwork;
            ViewBag.SuggestTopUpAfterCreate = !hasMainNetwork
                && hasInitialPricing
                && hasRenewalPricing
                && initialPriceSyp > 0m;
            ViewBag.NetworkPricingDeferredOnCreate = !hasMainNetwork && shouldChargeNow && initialPriceSyp > 0m;
        }

        private static bool IsNetworkPricingConfigured(FeaturePricing? initial, FeaturePricing? renewal) =>
            initial != null &&
            initial.BillingPeriod == PricingBillingPeriod.OneTime &&
            renewal != null &&
            renewal.BillingPeriod != PricingBillingPeriod.OneTime;
    }
}
