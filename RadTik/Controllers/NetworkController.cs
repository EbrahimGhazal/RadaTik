using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Helpers;
using RadTik.Security;
using RadTik.Services;
using RadTik.ViewModels.Network;
using System;

namespace RadTik.Controllers
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

        public NetworkController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<NetworkController> logger,
            IWebHostEnvironment environment,
            IUsageBasedSubscriptionChargeService usageSubscriptionChargeService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _environment = environment;
            _usageSubscriptionChargeService = usageSubscriptionChargeService;
        }

        // GET: Network
        // مدير النظام: عرض الشبكات الرئيسية مع مدير الشركة والرصيد. مدير الشركة: عرض شبكته.
        public async Task<IActionResult> Index(string? status = null, string? q = null, string? scope = null)
        {
            var isSysAdmin = User.IsInRole("SystemAdministrator");
            var user = await _userManager.GetUserAsync(User);
            const string ScopeSessionKey = "NetworkIndexScopeFilter";

            // Remember last selected scope filter in session (all/main/sub).
            if (!string.IsNullOrWhiteSpace(scope))
            {
                var normalizedScope = scope.Trim().ToLowerInvariant();
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
                query = query.Where(n => n.Id == user.NetworkId.Value);

            var all = await query.OrderBy(n => n.Name).ToListAsync();

            var naIds = new HashSet<string>();
            if (isSysAdmin)
            {
                var nas = await _userManager.GetUsersInRoleAsync("NetworkAdministrator");
                foreach (var u in nas) naIds.Add(u.Id);
            }

            var managerNames = new Dictionary<int, string>();
            foreach (var n in all)
            {
                var name = "-";
                if (n.ManagerUserId != null && n.ManagerUser != null)
                    name = n.ManagerUser.FullName ?? n.ManagerUser.UserName ?? "-";
                else if (isSysAdmin && n.Users != null)
                {
                    var mgr = n.Users.FirstOrDefault(u => naIds.Contains(u.Id));
                    if (mgr != null) name = mgr.FullName ?? mgr.UserName ?? "-";
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

            var companies = all;
            if (!string.IsNullOrEmpty(ViewBag.StatusFilter))
            {
                var sf = (string)ViewBag.StatusFilter;
                if (Enum.TryParse<NetworkStatus>(sf, ignoreCase: true, out var st))
                    companies = companies.Where(c => c.Status == st).ToList();
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var term = q.Trim();
                companies = companies
                    .Where(c =>
                        (c.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) == true) ||
                        (managerNames.TryGetValue(c.Id, out var mgr) && mgr.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    .ToList();
            }

            return View(companies);
        }

        // GET: Network/Create — مدير النظام لا يمكنه إضافة شبكات
        public async Task<IActionResult> Create()
        {
            if (User.IsInRole("SystemAdministrator"))
                return Forbid();

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return RedirectToAction("Login", "Account");

            await PopulateNetworkPricingPreviewAsync(user);
            return View();
        }

        // POST: Network/Create — مدير النظام لا يمكنه إضافة شبكات
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(NetworkViewModel model, IFormFile? logoFile)
        {
            if (User.IsInRole("SystemAdministrator"))
                return Forbid();

            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null)
                return RedirectToAction("Login", "Account");

            if (ImageUploadRules.IsTooLarge(logoFile))
            {
                ModelState.AddModelError(nameof(model.LogoFile), ImageUploadRules.MaxNetworkLogoSizeMessage);
            }

            if (ModelState.IsValid)
            {
                var isMain = !currentUser.NetworkId.HasValue;
                var network = new Network
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
                    network.ManagerUserId = currentUser.Id;

                // رفع الشعار إذا تم تحميله
                if (logoFile != null && logoFile.Length > 0)
                {
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "networks");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + logoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(fileStream);
                    }

                    network.LogoPath = $"/uploads/networks/{uniqueFileName}";
                }

                var (initialNetworkPricing, renewalNetworkPricing) = await GetNetworkPricingSettingsAsync();
                var companyNetworkIdForBilling = currentUser.NetworkId ?? 0;
                var companyNetworkForBilling = default(Network);
                var oneTimeChargeAmount = 0m;

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

                    if (initialNetworkPricing == null || initialNetworkPricing.BillingPeriod != PricingBillingPeriod.OneTime)
                    {
                        ModelState.AddModelError(string.Empty,
                            "لم يقم مدير النظام بتحديد سعر إنشاء الشبكات الإضافية بعد. يرجى مراجعة تبويب الأسعار والتجديد.");
                        await PopulateNetworkPricingPreviewAsync(currentUser);
                        return View(model);
                    }

                    if (renewalNetworkPricing == null || renewalNetworkPricing.BillingPeriod == PricingBillingPeriod.OneTime)
                    {
                        ModelState.AddModelError(string.Empty,
                            "لم يقم مدير النظام بتحديد إعدادات تجديد اشتراك الشبكات بعد. يرجى مراجعة تبويب الأسعار والتجديد.");
                        await PopulateNetworkPricingPreviewAsync(currentUser);
                        return View(model);
                    }

                    oneTimeChargeAmount = WalletMath.CeilSyp(initialNetworkPricing.AmountSYP);
                    if (oneTimeChargeAmount < 0)
                    {
                        oneTimeChargeAmount = 0m;
                    }

                    if (oneTimeChargeAmount > 0 && companyNetworkForBilling.Balance < oneTimeChargeAmount)
                    {
                        ModelState.AddModelError(string.Empty,
                            $"الرصيد غير كافٍ لإنشاء شبكة إضافية. المطلوب: {oneTimeChargeAmount:N2} ل.س.ج، الرصيد الحالي: {companyNetworkForBilling.Balance:N2} ل.س.ج.");
                        await PopulateNetworkPricingPreviewAsync(currentUser);
                        return View(model);
                    }
                }

                try
                {
                    await using var tx = await _context.Database.BeginTransactionAsync();

                    // التحقق من عدم وجود شبكة بنفس الاسم
                    var existingNetwork = await _context.Networks
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

                    var now = DateTime.Now;
                    var companyNetworkId = isMain ? network.Id : companyNetworkIdForBilling;

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
                    }
                    else if (companyNetworkForBilling != null && oneTimeChargeAmount > 0)
                    {
                        var previousBalance = companyNetworkForBilling.Balance;
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

                    TempData["Success"] = "تم إنشاء الشبكة بنجاح! يمكنك الآن البدء في إدارة المخدمات والقطاعات والمستقبلات والعملاء والبروفايلات.";
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
                        ModelState.AddModelError(string.Empty, "حدث خطأ أثناء حفظ البيانات. الرجاء المحاولة مرة أخرى.");
                    }

                    await PopulateNetworkPricingPreviewAsync(currentUser);
                    return View(model);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ غير متوقع في إنشاء الشبكة");
                    ModelState.AddModelError(string.Empty, "حدث خطأ غير متوقع. الرجاء المحاولة مرة أخرى أو الاتصال بالدعم الفني.");
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
                return Forbid();
            if (id == null)
                return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, id.Value))
                return Forbid();

            var network = await _context.Networks.FindAsync(id);
            if (network == null)
            {
                return NotFound();
            }

            var model = new NetworkViewModel
            {
                Id = network.Id,
                Name = network.Name,
                Governorates = network.Governorates,
                LogoPath = network.LogoPath,
                Status = network.Status,
                Notes = network.Notes
            };

            return View(model);
        }

        // POST: Network/Edit/5 — مدير النظام لا يمكنه تعديل بيانات الشبكة
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, NetworkViewModel model, IFormFile? logoFile)
        {
            if (User.IsInRole("SystemAdministrator"))
                return Forbid();
            if (id != model.Id)
                return NotFound();

            if (ImageUploadRules.IsTooLarge(logoFile))
            {
                ModelState.AddModelError(nameof(model.LogoFile), ImageUploadRules.MaxNetworkLogoSizeMessage);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, id))
                return Forbid();

            if (ModelState.IsValid)
            {
                var network = await _context.Networks.FindAsync(id);
                if (network == null)
                {
                    return NotFound();
                }

                network.Name = model.Name;
                network.Governorates = model.Governorates;
                network.Status = model.Status;
                network.Notes = model.Notes;

                // تحديث الشعار إذا تم تحميل ملف جديد
                if (logoFile != null && logoFile.Length > 0)
                {
                    // حذف الشعار القديم إن وجد
                    if (!string.IsNullOrEmpty(network.LogoPath))
                    {
                        var oldFilePath = Path.Combine(_environment.WebRootPath, network.LogoPath.TrimStart('/'));
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            System.IO.File.Delete(oldFilePath);
                        }
                    }

                    // رفع الشعار الجديد
                    var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "networks");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }

                    var uniqueFileName = Guid.NewGuid().ToString() + "_" + logoFile.FileName;
                    var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await logoFile.CopyToAsync(fileStream);
                    }

                    network.LogoPath = $"/uploads/networks/{uniqueFileName}";
                }

                try
                {
                    _context.Update(network);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "تم تحديث بيانات الشبكة بنجاح";
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

            var user = await _userManager.GetUserAsync(User);
            if (user == null || !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, id.Value))
            {
                return Forbid();
            }

            var network = await _context.Networks
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

            var mainId = network.ParentNetworkId ?? network.Id;
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
            var main = await _context.Networks
                .Include(n => n.ManagerUser)
                .Include(n => n.Users)
                .FirstOrDefaultAsync(n => n.Id == networkId && n.ParentNetworkId == null);
            if (main == null)
                return NotFound();

            ApplicationUser? manager = main.ManagerUserId != null ? main.ManagerUser : null;
            if (manager == null && main.Users != null)
            {
                var naIds = (await _userManager.GetUsersInRoleAsync("NetworkAdministrator")).Select(u => u.Id).ToHashSet();
                manager = main.Users.FirstOrDefault(u => naIds.Contains(u.Id));
            }
            if (manager == null)
            {
                TempData["Error"] = "لم يتم العثور على مدير شركة لهذه الشبكة.";
                return RedirectToAction(nameof(Index));
            }

            manager.IsActive = isActive;
            await _userManager.UpdateAsync(manager);

            var status = isActive ? NetworkStatus.Active : NetworkStatus.Inactive;
            var mainId = main.Id;
            var toUpdate = await _context.Networks
                .Where(n => n.Id == mainId || n.ParentNetworkId == mainId)
                .ToListAsync();
            foreach (var n in toUpdate)
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
                return NotFound();
            var main = await _context.Networks
                .Include(n => n.ManagerUser)
                .FirstOrDefaultAsync(n => n.Id == id && n.ParentNetworkId == null);
            if (main == null)
                return NotFound();
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
            var main = await _context.Networks
                .FirstOrDefaultAsync(n => n.Id == networkId && n.ParentNetworkId == null);
            if (main == null)
                return NotFound();
            main.Balance += amount;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"تم إضافة رصيد {amount:N2} بنجاح. الرصيد الحالي: {main.Balance:N2}.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GetManagerNameForNetworkAsync(int mainNetworkId)
        {
            var main = await _context.Networks
                .Include(n => n.ManagerUser)
                .Include(n => n.Users)
                .FirstOrDefaultAsync(n => n.Id == mainNetworkId && n.ParentNetworkId == null);
            if (main == null) return "-";
            if (main.ManagerUserId != null && main.ManagerUser != null)
                return main.ManagerUser.FullName ?? main.ManagerUser.UserName ?? "-";
            var naIds = (await _userManager.GetUsersInRoleAsync("NetworkAdministrator")).Select(u => u.Id).ToHashSet();
            var mgr = main.Users?.FirstOrDefault(u => naIds.Contains(u.Id));
            return mgr != null ? (mgr.FullName ?? mgr.UserName ?? "-") : "-";
        }

        // GET: Network/SelectNetwork - إعادة توجيه عند الوصول بالرابط مباشرة (تفادي HTTP 405)
        [HttpGet]
        public IActionResult SelectNetwork()
        {
            if (User.IsInRole(RoleNames.NetworkAdministrator))
                return RedirectToRoute("networkManager-dashboard");
            if (User.IsInRole(RoleNames.SystemAdministrator))
                return RedirectToRoute("systemAdmin-actions");
            return RedirectToRoute("networkManager-dashboard");
        }

        // POST: Network/SelectNetwork
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SelectNetwork(int networkId)
        {
            var user = _userManager.GetUserAsync(User).Result;
            if (user == null)
            {
                return RedirectToAction("Login", "Account");
            }

            // التحقق من أن المستخدم لديه صلاحية على هذه الشبكة
            var allowed = NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, networkId).Result;
            if (allowed)
            {
                NetworkHelper.SetCurrentNetworkId(HttpContext, networkId);
                TempData["Success"] = "تم تحديد الشبكة بنجاح";
            }
            else
            {
                TempData["Error"] = "ليس لديك صلاحية على هذه الشبكة";
            }

            // إعادة التوجيه إلى لوحة التحكم المناسبة حسب دور المستخدم
            if (User.IsInRole(RoleNames.NetworkAdministrator))
                return RedirectToRoute("networkManager-dashboard");
            if (User.IsInRole(RoleNames.SystemAdministrator))
                return RedirectToRoute("systemAdmin-actions");
            return RedirectToRoute("networkManager-dashboard");
        }

        private bool NetworkExists(int id)
        {
            return _context.Networks.Any(e => e.Id == id);
        }

        private async Task<(FeaturePricing? Initial, FeaturePricing? Renewal)> GetNetworkPricingSettingsAsync()
        {
            var pricingRows = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == NetworksFeatureKey &&
                    p.ChargeUnit == PricingChargeUnit.PerNetwork)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var initial = pricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
            var renewal = pricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
            return (initial, renewal);
        }

        private async Task EnsureNetworksSubscriptionAsync(
            int companyNetworkId,
            PricingBillingPeriod renewalBillingPeriod,
            DateTime now,
            CancellationToken ct)
        {
            var sub = await _context.NetworkServiceSubscriptions
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
            var (initialNetworkPricing, renewalNetworkPricing) = await GetNetworkPricingSettingsAsync();

            var hasMainNetwork = user.NetworkId.HasValue;
            var companyNetworkName = "شبكة جديدة";
            var totalCompanyNetworks = hasMainNetwork ? 1 : 0;

            if (hasMainNetwork)
            {
                var selected = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == user.NetworkId!.Value);

                if (selected != null)
                {
                    var companyNetworkId = selected.ParentNetworkId ?? selected.Id;
                    var company = selected.ParentNetworkId.HasValue
                        ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == companyNetworkId)
                        : selected;

                    companyNetworkName = company?.Name ?? selected.Name ?? "غير محدد";
                    totalCompanyNetworks = await _context.Networks
                        .AsNoTracking()
                        .CountAsync(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId);
                }
            }

            ViewBag.NetworkPricingIsFirstFree = !hasMainNetwork;
            ViewBag.NetworkPricingCompanyName = companyNetworkName;
            ViewBag.NetworkPricingTotalNetworks = totalCompanyNetworks;
            ViewBag.NetworkPricingHasInitial = initialNetworkPricing != null;
            ViewBag.NetworkPricingHasRenewal = renewalNetworkPricing != null;
            ViewBag.NetworkPricingInitialSyp = initialNetworkPricing != null
                ? WalletMath.CeilSyp(initialNetworkPricing.AmountSYP)
                : 0m;
            ViewBag.NetworkPricingRenewalSyp = renewalNetworkPricing != null
                ? WalletMath.CeilSyp(renewalNetworkPricing.AmountSYP)
                : 0m;
            ViewBag.NetworkPricingRenewalPeriodLabel = renewalNetworkPricing != null
                ? PricingDisplay.BillingPeriodLabel(renewalNetworkPricing.BillingPeriod)
                : "غير محدد";
        }
    }
}
