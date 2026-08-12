using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPreview;
using RadaTik.Services.SystemAdminPricing;
using RadaTik.Helpers;
using RadaTik.Security;
using RadaTik.ViewModels.MikroTikServers;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Storage;

namespace RadaTik.Controllers
{
    [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,EmployeeLegacy")]
    [RequirePermission("MikroTikServers.View")]
    public class MikroTikServersController : Controller
    {
        private const string MikroTikServersFeatureKey = FeatureKeys.MikroTikServers;
        private readonly ApplicationDbContext _context;
        private readonly IMikroTikPppoeUserService _mikroTikPppoe;
        private readonly IMikroTikUserImportService _mikroTikImport;
        private readonly IClientImportOrchestrator _clientImport;
        private readonly IMikroTikProfilesService _mikroTikProfilesService;
        private readonly ILogger<MikroTikServersController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ICreatePricingPreviewService _pricingPreviewService;

        public MikroTikServersController(
            ApplicationDbContext context,
            IMikroTikPppoeUserService mikroTikPppoe,
            IMikroTikUserImportService mikroTikImport,
            IClientImportOrchestrator clientImport,
            IMikroTikProfilesService mikroTikProfilesService,
            ILogger<MikroTikServersController> logger,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ICreatePricingPreviewService pricingPreviewService)
        {
            _context = context;
            _mikroTikPppoe = mikroTikPppoe;
            _mikroTikImport = mikroTikImport;
            _clientImport = clientImport;
            _mikroTikProfilesService = mikroTikProfilesService;
            _logger = logger;
            _userManager = userManager;
            _usageChargeService = usageChargeService;
            _pricingPreviewService = pricingPreviewService;
        }

        // GET: MikroTikServers
        public async Task<IActionResult> Index(int? selectedNetworkId)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            List<Network> networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            HashSet<int> accessibleNetworkIds = networks.Select(n => n.Id).ToHashSet();
            bool showAllNetworks = selectedNetworkId.HasValue && selectedNetworkId.Value == 0;
            int effectiveNetworkId = selectedNetworkId.HasValue && selectedNetworkId.Value > 0
                ? selectedNetworkId.Value
                : networkId.Value;

            if (!showAllNetworks && !accessibleNetworkIds.Contains(effectiveNetworkId))
            {
                TempData["Error"] = "الشبكة المحددة غير متاحة لك.";
                return RedirectToAction(nameof(Index));
            }

            IQueryable<MikroTikServer> serversQuery = _context.MikroTikServers
                .Include(s => s.Network)
                .AsQueryable();

            if (showAllNetworks)
            {
                serversQuery = serversQuery.Where(s => s.NetworkId.HasValue && accessibleNetworkIds.Contains(s.NetworkId.Value));
            }
            else
            {
                serversQuery = serversQuery.Where(s => s.NetworkId == effectiveNetworkId);
            }

            List<MikroTikServer> servers = await serversQuery.ToListAsync();

            ViewBag.Networks = networks;
            ViewBag.CurrentNetworkId = networkId;
            ViewBag.SelectedNetworkId = showAllNetworks ? 0 : effectiveNetworkId;
            ViewBag.ShowAllNetworks = showAllNetworks;
            ViewBag.IsAllNetworksSelection = showAllNetworks;
            if (!showAllNetworks)
            {
                await PopulateServerRenewalSummaryAsync(effectiveNetworkId);
            }
            else
            {
                int activeServersCount = servers.Count(s => s.IsActive);
                ViewBag.ServerRenewalHasPricing = false;
                ViewBag.ServerRenewalPeriodLabel = "غير متاح عند اختيار كل الشبكات";
                ViewBag.ServerRenewalActiveServers = activeServersCount;
                ViewBag.ServerRenewalAdditionalServers = Math.Max(0, activeServersCount - 1);
                ViewBag.ServerRenewalUnitPriceSyp = 0m;
                ViewBag.ServerRenewalEstimatedAmountSyp = 0m;
            }

            return View(servers);
        }

        // GET: MikroTikServers/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? mikrotikServer = await _context.MikroTikServers
                .FirstOrDefaultAsync(m => m.Id == id && m.NetworkId == networkId.Value);
            if (mikrotikServer == null)
            {
                return NotFound();
            }

            await PopulateServerRenewalSummaryAsync(networkId.Value);
            return View(mikrotikServer);
        }

        // GET: MikroTikServers/Create
        [RequirePermission("MikroTikServers.Create")]
        public async Task<IActionResult> Create()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // جلب قائمة الشبكات المتاحة للمستخدم
            List<Network> networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            bool requiresExplicitNetworkSelection = networks.Count > 1;
            ViewBag.Networks = new SelectList(networks, "Id", "Name", requiresExplicitNetworkSelection ? null : networkId);
            ViewBag.CurrentNetworkId = networkId;
            ViewBag.RequiresExplicitNetworkSelection = requiresExplicitNetworkSelection;
            await PopulateServerPricingPreviewAsync(user!, networkId.Value);

            return View();
        }

        // POST: MikroTikServers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MikroTikServers.Create")]
        public async Task<IActionResult> Create([Bind("Name,Host,Port,User,Pass,Notes,UserID,IsActive,NetworkId")] MikroTikServer mikrotikServer)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction("Index", "Network");
                }

                if (!mikrotikServer.NetworkId.HasValue)
                {
                    ModelState.AddModelError("NetworkId", "يرجى تحديد الشبكة التي سيتم إضافة السيرفر لها.");
                }

                int selectedNetworkId = mikrotikServer.NetworkId ?? 0;
                if (mikrotikServer.NetworkId.HasValue)
                {
                    bool hasAccess = await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, selectedNetworkId);
                    if (!hasAccess)
                    {
                        ModelState.AddModelError("NetworkId", "الشبكة المحددة غير متاحة لك.");
                    }
                }

                (FeaturePricing? initialServerPricing, FeaturePricing? renewalServerPricing) = await GetServerPricingSettingsAsync();
                RecurringPricingPolicy recurringPolicy = RecurringPricingPolicyCodec.ReadFromPricings(initialServerPricing, renewalServerPricing);
                Network? selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;
                List<int> scopeNetworkIds = await _context.Networks
                    .AsNoTracking()
                    .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
                    .Select(n => n.Id)
                    .ToListAsync();

                int currentServersCount = await _context.MikroTikServers
                    .AsNoTracking()
                    .CountAsync(s => s.IsActive && s.NetworkId.HasValue && scopeNetworkIds.Contains(s.NetworkId.Value));
                bool shouldChargeNow = currentServersCount >= recurringPolicy.FreeInitialUnits;
                decimal oneTimeChargeAmount = initialServerPricing != null
                    ? WalletMath.CeilSyp(initialServerPricing.AmountSYP)
                    : 0m;

                if (shouldChargeNow)
                {
                    if (initialServerPricing == null || initialServerPricing.BillingPeriod != PricingBillingPeriod.OneTime)
                    {
                        ModelState.AddModelError(string.Empty,
                            AppMessages.PricingNotConfigured);
                    }

                    if (renewalServerPricing == null || renewalServerPricing.BillingPeriod == PricingBillingPeriod.OneTime)
                    {
                        ModelState.AddModelError(string.Empty,
                            AppMessages.PricingNotConfigured);
                    }
                }

                if (ModelState.IsValid)
                {
                    await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();

                    // تعيين التواريخ
                    mikrotikServer.CreatedAt = DateTime.Now;
                    mikrotikServer.UpdatedAt = DateTime.Now;

                    // إذا لم يتم تحديد اسم، استخدم المضيف كاسم
                    if (string.IsNullOrWhiteSpace(mikrotikServer.Name))
                    {
                        mikrotikServer.Name = mikrotikServer.Host;
                    }

                    // تعيين الشبكة
                    mikrotikServer.NetworkId = selectedNetworkId;

                    // منع التكرار فقط عند تطابق الشبكة + المضيف + المنفذ معًا
                    string hostKey = (mikrotikServer.Host ?? string.Empty).Trim();
                    MikroTikServer? existingServer = await _context.MikroTikServers
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s =>
                            s.NetworkId == selectedNetworkId
                            && s.Port == mikrotikServer.Port
                            && s.Host.ToLower() == hostKey.ToLower());

                    if (existingServer != null)
                    {
                        ModelState.AddModelError(string.Empty,
                            "يوجد بالفعل خادم بنفس اسم المضيف ونفس المنفذ في هذه الشبكة. غيّر المضيف أو المنفذ أو الشبكة.");
                        await tx.RollbackAsync();
                        await RebuildCreateViewStateAsync(user!, selectedNetworkId);
                        return View(mikrotikServer);
                    }

                    mikrotikServer.Host = hostKey;

                    DateTime now = DateTime.Now;
                    Network? companyNetwork = await _context.Networks
                        .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null);

                    if (companyNetwork == null)
                    {
                        ModelState.AddModelError(string.Empty, "تعذر تحديد حساب الشركة الرئيسي.");
                        await tx.RollbackAsync();
                        await RebuildCreateViewStateAsync(user!, selectedNetworkId);
                        return View(mikrotikServer);
                    }

                    if (shouldChargeNow && oneTimeChargeAmount > 0 && companyNetwork.Balance < oneTimeChargeAmount)
                    {
                        ModelState.AddModelError(string.Empty,
                            AppMessages.InsufficientBalance);
                        await tx.RollbackAsync();
                        await RebuildCreateViewStateAsync(user!, selectedNetworkId);
                        return View(mikrotikServer);
                    }

                    _context.Add(mikrotikServer);
                    await _context.SaveChangesAsync();

                    if (renewalServerPricing != null && renewalServerPricing.BillingPeriod != PricingBillingPeriod.OneTime)
                    {
                        await EnsureServerSubscriptionAsync(
                            companyNetworkId,
                            renewalServerPricing.BillingPeriod,
                            now,
                            HttpContext.RequestAborted);
                    }

                    if (shouldChargeNow && oneTimeChargeAmount > 0)
                    {
                        decimal previousBalance = companyNetwork.Balance;
                        companyNetwork.Balance -= oneTimeChargeAmount;
                        _context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                        {
                            NetworkId = companyNetworkId,
                            Type = NetworkWalletTransactionType.ServiceCharge,
                            SignedAmount = -oneTimeChargeAmount,
                            PreviousBalance = previousBalance,
                            NewBalance = companyNetwork.Balance,
                            CreatedByUserId = user!.Id,
                            CreatedAt = now,
                            Notes = $"إنشاء سيرفر إضافي: {mikrotikServer.Name} ({MikroTikServersFeatureKey} / {PricingBillingPeriod.OneTime} / {PricingChargeUnit.PerServer})"
                        });
                    }

                    await _context.SaveChangesAsync();
                    await tx.CommitAsync();

                    TempData["Success"] = AppMessages.OperationSuccess;
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "خطأ في حفظ بيانات الخادم");
                ModelState.AddModelError(string.Empty,
                    "تعذر الحفظ. تحقق من عدم تكرار نفس المضيف ونفس المنفذ في نفس الشبكة.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع");
                ModelState.AddModelError(string.Empty, AppMessages.UnexpectedError);
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? currentNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
            if (currentUser != null && currentNetworkId.HasValue)
            {
                await RebuildCreateViewStateAsync(currentUser, mikrotikServer.NetworkId ?? currentNetworkId.Value);
            }

            return View(mikrotikServer);
        }

        // GET: MikroTikServers/Edit/5
        [RequirePermission("MikroTikServers.Edit")]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? mikrotikServer = await _context.MikroTikServers
                .FirstOrDefaultAsync(m => m.Id == id && m.NetworkId == networkId.Value);
            if (mikrotikServer == null)
            {
                return NotFound();
            }
            return View(mikrotikServer);
        }

        // POST: MikroTikServers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MikroTikServers.Edit")]
        public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Host,Port,User,Pass,Notes,UserID,IsActive,CreatedAt")] MikroTikServer mikrotikServer)
        {
            if (id != mikrotikServer.Id)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // التحقق من أن الخادم يتبع شبكة المستخدم (وجلب الكيان المُتتبَّع لتحديثه)
            MikroTikServer? existingServer = await _context.MikroTikServers
                .FirstOrDefaultAsync(m => m.Id == id && m.NetworkId == networkId.Value);
            if (existingServer == null)
            {
                return NotFound();
            }

            // إذا ترك المستخدم كلمة المرور فارغة = الإبقاء على الكلمة الحالية
            if (string.IsNullOrWhiteSpace(mikrotikServer.Pass))
            {
                mikrotikServer.Pass = existingServer.Pass;
                ModelState.Remove("Pass");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    string hostKey = (mikrotikServer.Host ?? string.Empty).Trim();
                    bool duplicateEndpoint = await _context.MikroTikServers
                        .AsNoTracking()
                        .AnyAsync(s =>
                            s.Id != id
                            && s.NetworkId == networkId.Value
                            && s.Port == mikrotikServer.Port
                            && s.Host.ToLower() == hostKey.ToLower());
                    if (duplicateEndpoint)
                    {
                        ModelState.AddModelError(string.Empty,
                            "يوجد بالفعل خادم بنفس اسم المضيف ونفس المنفذ في هذه الشبكة. غيّر المضيف أو المنفذ.");
                        return View(mikrotikServer);
                    }

                    existingServer.Name = mikrotikServer.Name;
                    existingServer.Host = hostKey;
                    existingServer.Port = mikrotikServer.Port;
                    existingServer.User = mikrotikServer.User;
                    existingServer.Pass = mikrotikServer.Pass;
                    existingServer.Notes = mikrotikServer.Notes;
                    existingServer.UserID = mikrotikServer.UserID;
                    existingServer.IsActive = mikrotikServer.IsActive;
                    existingServer.CreatedAt = mikrotikServer.CreatedAt;
                    existingServer.NetworkId = networkId.Value;
                    existingServer.UpdatedAt = DateTime.Now;

                    await _context.SaveChangesAsync();

                    TempData["Success"] = AppMessages.OperationSuccess;
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MikroTikServerExists(mikrotikServer.Id))
                    {
                        return NotFound();
                    }
                    ModelState.AddModelError(string.Empty, "تم تعديل السجل من جهة أخرى. أعد تحميل الصفحة وحاول مرة أخرى.");
                    return View(mikrotikServer);
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "خطأ في تحديث الخادم {Id}", id);
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تحديث البيانات. تحقق من عدم تكرار المضيف والمنفذ في نفس الشبكة.");
                    return View(mikrotikServer);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "خطأ في تحديث الخادم {Id}", id);
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تحديث البيانات.");
                    return View(mikrotikServer);
                }
            }
            return View(mikrotikServer);
        }

        // GET: MikroTikServers/Delete/5
        [RequirePermission("MikroTikServers.Delete")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? mikrotikServer = await _context.MikroTikServers
                .FirstOrDefaultAsync(m => m.Id == id && m.NetworkId == networkId.Value);
            if (mikrotikServer == null)
            {
                return NotFound();
            }

            return View(mikrotikServer);
        }

        // POST: MikroTikServers/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [RequirePermission("MikroTikServers.Delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (user == null || !networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction("Index", "Network");
                }

                MikroTikServer? mikrotikServer = await _context.MikroTikServers
                    .FirstOrDefaultAsync(m => m.Id == id && m.NetworkId == networkId.Value);
                if (mikrotikServer != null)
                {
                    // التحقق من وجود قطاعات مرتبطة بهذا الخادم
                    bool hasSectors = await _context.Sectors.AnyAsync(s => s.MikroTikServerId == id);

                    if (hasSectors)
                    {
                        TempData["Error"] = "❌ لا يمكن حذف هذا الخادم لأنه مرتبط بقطاعات. الرجاء إزالة القطاعات أولاً.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.MikroTikServers.Remove(mikrotikServer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = AppMessages.OperationSuccess;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في حذف الخادم");
                TempData["Error"] = "❌ حدث خطأ أثناء حذف الخادم";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool MikroTikServerExists(int id)
        {
            return _context.MikroTikServers.Any(e => e.Id == id);
        }

        // GET: MikroTikServers/ActiveUsers/5
        public async Task<IActionResult> ActiveUsers(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);

            if (server == null)
            {
                return NotFound();
            }

            try
            {
                List<Client> activeUsers = await _mikroTikPppoe.GetActivePPPoEUsers(id.Value);
                ViewData["ServerName"] = string.IsNullOrWhiteSpace(server.Name) ? server.Host : server.Name;
                ViewData["ServerId"] = id.Value;
                return View(activeUsers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = BuildFriendlyMikroTikFetchError("جلب البيانات", ex);
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }

        // GET: MikroTikServers/AllUsers/5
        public async Task<IActionResult> AllUsers(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);

            if (server == null)
            {
                return NotFound();
            }

            try
            {
                List<EditMikroTikUserViewModel> allUsers = await _mikroTikPppoe.GetAllUsersWithDetails(id.Value);
                ViewData["ServerName"] = string.IsNullOrWhiteSpace(server.Name) ? server.Host : server.Name;
                ViewData["ServerId"] = id.Value;

                MikroTikServerUsersImportContext importContext =
                    await _clientImport.BuildServerUsersImportContextAsync(id.Value, networkId.Value);
                ViewData["ImportPreview"] = importContext.Preview;
                ViewData["ImportEstimate"] = importContext.Estimate;
                ViewData["ClientImportUnitPrice"] = importContext.SubscriberUnitPrice;

                // تصفية المستقبلات حسب الشبكة
                List<Receiver> receivers = await _context.Receivers
                    .Where(r => r.NetworkId == networkId.Value)
                    .ToListAsync();
                ViewData["Receivers"] = new SelectList(receivers, "Id", "Name");

                return View(allUsers);
            }
            catch (Exception ex)
            {
                TempData["Error"] = BuildFriendlyMikroTikFetchError("جلب البيانات", ex);
                return RedirectToAction(nameof(Details), new { id = id });
            }
        }

        // POST: MikroTikServers/ImportAllUsersToDatabase
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("Clients.ImportFromServer")]
        public async Task<IActionResult> ImportAllUsersToDatabase(int serverId)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            // التأكد من أن الخادم يتبع نفس الشبكة
            MikroTikServer? server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

            if (server == null)
            {
                return NotFound();
            }

            try
            {
                ClientImportOutcome outcome = await _clientImport.ExecuteImportAsync(
                    serverId,
                    networkId.Value,
                    user!.Id,
                    rejectWhenProfilesMissing: true);
                ApplyClientImportOutcome(outcome);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في استيراد جميع المستخدمين من الخادم {ServerId}", serverId);
                TempData["Error"] = $"❌ خطأ في استيراد المستخدمين: {ex.Message}";
            }

            return RedirectToAction(nameof(AllUsers), new { id = serverId });
        }

        // GET: MikroTikServers/EditUser/5?userName=test
        [RequirePermission("MikroTikServers.Edit")]
        public async Task<IActionResult> EditUser(int? id, string userName)
        {
            if (id == null || string.IsNullOrEmpty(userName))
            {
                TempData["Error"] = "معرف السيرفر أو اسم المستخدم غير صالح";
                return RedirectToAction(nameof(Index));
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);

            if (server == null)
            {
                TempData["Error"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // جلب بيانات المستخدم من MikroTik
                List<EditMikroTikUserViewModel> allUsers = await _mikroTikPppoe.GetAllUsersWithDetails(id.Value);
                EditMikroTikUserViewModel? mikrotikUser = allUsers.FirstOrDefault(u => u.UserName == userName);

                if (mikrotikUser == null)
                {
                    TempData["Error"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(AllUsers), new { id = id });
                }

                // جلب البروفايلات من السيرفر الحالي
                List<string> profiles = await _mikroTikProfilesService.GetProfileNamesFromMikroTik(id.Value);

                // تحويل القائمة إلى SelectListItem
                List<SelectListItem> profileItems = profiles.Select(p => new SelectListItem
                {
                    Text = p,
                    Value = p,
                    Selected = (p == mikrotikUser.ProfileName)
                }).ToList();

                // إضافة خيار افتراضي في البداية
                profileItems.Insert(0, new SelectListItem
                {
                    Text = "-- اختر البروفايل --",
                    Value = "",
                    Selected = string.IsNullOrEmpty(mikrotikUser.ProfileName)
                });

                ViewBag.ServerName = string.IsNullOrWhiteSpace(server.Name) ? server.Host : server.Name;
                ViewBag.ServerId = id.Value;

                // تصفية المستقبلات حسب الشبكة
                List<Receiver> receivers = await _context.Receivers
                    .Where(r => r.NetworkId == networkId.Value)
                    .ToListAsync();
                ViewBag.Receivers = new SelectList(receivers, "Id", "Name", mikrotikUser.ReceiverId);
                ViewBag.Profiles = profileItems;

                return View(mikrotikUser);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب بيانات المستخدم");
                TempData["Error"] = BuildFriendlyMikroTikFetchError("جلب بيانات المستخدم", ex);
                return RedirectToAction(nameof(AllUsers), new { id = id });
            }
        }

        // POST: MikroTikServers/EditUser/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MikroTikServers.Edit")]
        public async Task<IActionResult> EditUser(int id, EditMikroTikUserViewModel model)
        {
            if (id != model.MikroTikServerId)
            {
                TempData["Error"] = "معرف السيرفر غير متطابق";
                return RedirectToAction(nameof(AllUsers), new { id = id });
            }

            if (!ModelState.IsValid)
            {
                // إذا كانت هناك أخطاء في التحقق، إعادة تحميل البيانات
                await ReloadViewBagData(id, model);

                // جمع جميع أخطاء التحقق لعرضها
                List<string> errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .SelectMany(x => x.Value?.Errors.Select(e => $"{x.Key}: {e.ErrorMessage}") ?? Enumerable.Empty<string>())
                    .ToList();

                if (errors.Count > 0)
                {
                    TempData["Error"] = "الرجاء تصحيح الأخطاء في النموذج: " + string.Join(" | ", errors);
                }
                else
                {
                    TempData["Error"] = "الرجاء تصحيح الأخطاء في النموذج";
                }

                return View(model);
            }

            try
            {
                // التحقق من أن البروفايل موجود في السيرفر
                if (!string.IsNullOrEmpty(model.ProfileName))
                {
                    List<string> profiles = await _mikroTikProfilesService.GetProfileNamesFromMikroTik(id);
                    if (!profiles.Contains(model.ProfileName))
                    {
                        ModelState.AddModelError("ProfileName", "البروفايل المحدد غير موجود في السيرفر");
                        await ReloadViewBagData(id, model);
                        TempData["Error"] = "البروفايل المحدد غير موجود في السيرفر";
                        return View(model);
                    }
                }

                // تحديث بيانات المستخدم
                bool result = await _mikroTikPppoe.UpdateUserFromAllUsers(model);

                if (result)
                {
                    TempData["Success"] = AppMessages.OperationSuccess;
                    return RedirectToAction(nameof(AllUsers), new { id = id });
                }
                else
                {
                    TempData["Error"] = "❌ فشل في تحديث بيانات العميل";
                    await ReloadViewBagData(id, model);
                    return View(model);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في تحديث بيانات العميل {UserName}", model.UserName);
                await ReloadViewBagData(id, model);
                TempData["Error"] = $"❌ خطأ في التعديل: {ex.Message}";
                return View(model);
            }
        }

        private async Task ReloadViewBagData(int serverId, EditMikroTikUserViewModel model)
        {
            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (networkId.HasValue)
            {
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server != null)
                {
                    ViewBag.ServerName = string.IsNullOrWhiteSpace(server.Name) ? server.Host : server.Name;
                }
            }

            ViewBag.ServerId = serverId;

            // تصفية المستقبلات حسب الشبكة
            if (networkId.HasValue)
            {
                List<Receiver> receivers = await _context.Receivers
                    .Where(r => r.NetworkId == networkId.Value)
                    .ToListAsync();
                ViewBag.Receivers = new SelectList(receivers, "Id", "Name", model.ReceiverId);
            }
            else
            {
                ViewBag.Receivers = new SelectList(_context.Receivers, "Id", "Name", model.ReceiverId);
            }

            try
            {
                // جلب البروفايلات من السيرفر
                List<string> profiles = await _mikroTikProfilesService.GetProfileNamesFromMikroTik(serverId);
                List<SelectListItem> profileItems = profiles.Select(p => new SelectListItem
                {
                    Text = p,
                    Value = p,
                    Selected = (p == model.ProfileName)
                }).ToList();

                profileItems.Insert(0, new SelectListItem
                {
                    Text = "-- اختر البروفايل --",
                    Value = "",
                    Selected = string.IsNullOrEmpty(model.ProfileName)
                });

                ViewBag.Profiles = profileItems;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ في جلب البروفايلات");
                ViewBag.Profiles = new List<SelectListItem>();
            }
        }


        // GET: MikroTikServers/TestConnection/5
        public async Task<IActionResult> TestConnection(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);

            if (server == null)
            {
                return NotFound();
            }

            try
            {
                bool isConnected = await _mikroTikPppoe.TestConnection(id.Value);
                if (isConnected)
                {
                    TempData["Success"] = AppMessages.OperationSuccess;
                }
                else
                {
                    TempData["Error"] = "✗ فشل الاتصال بالخادم";
                }
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"✗ خطأ في الاتصال: {ex.Message}";
            }

            return RedirectToAction(nameof(Details), new { id = id });
        }

        // GET: MikroTikServers/Debug/5 - صفحة تصحيح الأخطاء
        public async Task<IActionResult> Debug(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction("Index", "Network");
            }

            MikroTikServer? server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);

            if (server == null)
            {
                return NotFound();
            }

            return View(server);
        }

        // POST: MikroTikServers/FreezeUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MikroTikServers.Edit")]
        public async Task<IActionResult> FreezeUser(int serverId, string userName)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["Error"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                await _mikroTikPppoe.FreezeAccount(serverId, userName);

                // تحديث قاعدة البيانات إذا كان المستخدم موجودًا فيها (في نفس الشبكة)
                Client? client = await _context.Clients
                    .FirstOrDefaultAsync(c => c.UserName == userName && c.MikroTikServerId == serverId && c.NetworkId == networkId.Value);

                if (client != null)
                {
                    client.IsActive = false;
                    client.ConnectionStatus = "معطل";
                    client.LastUpdated = DateTime.Now;
                    _context.Update(client);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"✅ تم تجميد حساب {userName} بنجاح";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ خطأ في تجميد الحساب: {ex.Message}";
            }

            return RedirectToAction(nameof(AllUsers), new { id = serverId });
        }

        // POST: MikroTikServers/UnfreezeUser
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MikroTikServers.Edit")]
        public async Task<IActionResult> UnfreezeUser(int serverId, string userName)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["Error"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                await _mikroTikPppoe.UnfreezeAccount(serverId, userName);

                // تحديث قاعدة البيانات إذا كان المستخدم موجودًا فيها (في نفس الشبكة)
                Client? client = await _context.Clients
                    .FirstOrDefaultAsync(c => c.UserName == userName && c.MikroTikServerId == serverId && c.NetworkId == networkId.Value);

                if (client != null)
                {
                    client.IsActive = true;
                    client.ConnectionStatus = "مفعل";
                    client.LastUpdated = DateTime.Now;
                    _context.Update(client);
                    await _context.SaveChangesAsync();
                }

                TempData["Success"] = $"✅ تم تفعيل حساب {userName} بنجاح";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ خطأ في تفعيل الحساب: {ex.Message}";
            }

            return RedirectToAction(nameof(AllUsers), new { id = serverId });
        }

        private static string BuildFriendlyMikroTikFetchError(string operation, Exception ex)
        {
            string rawMessage = ex.Message ?? string.Empty;
            if (rawMessage.Contains("forcibly closed", StringComparison.OrdinalIgnoreCase) ||
                rawMessage.Contains("transport connection", StringComparison.OrdinalIgnoreCase) ||
                rawMessage.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
                rawMessage.Contains("connection reset", StringComparison.OrdinalIgnoreCase) ||
                rawMessage.Contains("socket", StringComparison.OrdinalIgnoreCase))
            {
                return
                    $"تعذر {operation} من خادم MikroTik لأن الاتصال انقطع. تحقق من صحة Host/Port، " +
                    "تفعيل API أو API-SSL، والسماح بالاتصال من السيرفر.";
            }

            return $"خطأ في {operation}: {rawMessage}";
        }

        private async Task RebuildCreateViewStateAsync(ApplicationUser user, int networkId)
        {
            List<Network> networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            bool requiresExplicitNetworkSelection = networks.Count > 1;
            ViewBag.Networks = new SelectList(networks, "Id", "Name", networkId);
            ViewBag.CurrentNetworkId = networkId;
            ViewBag.RequiresExplicitNetworkSelection = requiresExplicitNetworkSelection;
            await PopulateServerPricingPreviewAsync(user, networkId);
        }

        private async Task PopulateServerRenewalSummaryAsync(int selectedNetworkId)
        {
            Network? selected = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            int companyNetworkId = selected?.ParentNetworkId ?? selectedNetworkId;

            List<int> scopeNetworkIds = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
                .Select(n => n.Id)
                .ToListAsync();

            int activeServersCount = await _context.MikroTikServers
                .AsNoTracking()
                .CountAsync(s => s.IsActive && s.NetworkId.HasValue && scopeNetworkIds.Contains(s.NetworkId.Value));

            int additionalServersCount = Math.Max(0, activeServersCount - 1);
            (FeaturePricing? _, FeaturePricing? renewalServerPricing) = await GetServerPricingSettingsAsync();
            decimal renewalUnitPrice = renewalServerPricing != null ? WalletMath.CeilSyp(renewalServerPricing.AmountSYP) : 0m;

            ViewBag.ServerRenewalPeriodLabel = renewalServerPricing != null
                ? PricingDisplay.BillingPeriodLabel(renewalServerPricing.BillingPeriod)
                : "غير محدد";
            ViewBag.ServerRenewalHasPricing = renewalServerPricing != null;
            ViewBag.ServerRenewalActiveServers = activeServersCount;
            ViewBag.ServerRenewalAdditionalServers = additionalServersCount;
            ViewBag.ServerRenewalUnitPriceSyp = renewalUnitPrice;
            ViewBag.ServerRenewalEstimatedAmountSyp = WalletMath.CeilSyp(additionalServersCount * renewalUnitPrice);
        }

        private async Task PopulateServerPricingPreviewAsync(ApplicationUser user, int selectedNetworkId)
        {
            CreatePricingPreviewResult preview = await _pricingPreviewService.BuildAsync(
                selectedNetworkId,
                FeatureKeys.MikroTikServers,
                PricingChargeUnit.PerServer,
                PricingPreviewCounterKeys.MikroTikServers);
            PricingPreviewViewBagMapper.Apply(ViewData, "ServerPricing", preview);

            ViewBag.ServerPricingIsFirstFree = preview.TotalUnits == 0;
            ViewBag.ServerPricingCompanyName = preview.CompanyName;
            ViewBag.ServerPricingTotalServers = preview.TotalUnits;
            ViewBag.ServerPricingHasInitial = preview.HasInitialPricing;
            ViewBag.ServerPricingHasRenewal = preview.HasRenewalPricing;
            ViewBag.ServerPricingFreeInitialUnits = preview.FreeInitialUnits;
            ViewBag.ServerPricingFreeRenewalUnits = preview.FreeRenewalUnits;
            ViewBag.ServerPricingInitialSyp = preview.InitialPriceSyp;
            ViewBag.ServerPricingRenewalSyp = preview.RenewalPriceSyp;
            ViewBag.ServerPricingRenewalPeriodLabel = preview.RenewalPeriodLabel;
            ViewBag.ServerPricingShouldChargeNow = preview.ShouldChargeNow;
        }

        private async Task<(FeaturePricing? Initial, FeaturePricing? Renewal)> GetServerPricingSettingsAsync()
        {
            List<FeaturePricing> pricingRows = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == MikroTikServersFeatureKey &&
                    p.ChargeUnit == PricingChargeUnit.PerServer)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            FeaturePricing? initial = pricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
            FeaturePricing? renewal = pricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
            return (initial, renewal);
        }

        private async Task EnsureServerSubscriptionAsync(
            int companyNetworkId,
            PricingBillingPeriod renewalBillingPeriod,
            DateTime now,
            CancellationToken ct)
        {
            NetworkServiceSubscription? sub = await _context.NetworkServiceSubscriptions
                .FirstOrDefaultAsync(s => s.NetworkId == companyNetworkId && s.FeatureKey == MikroTikServersFeatureKey, ct);

            if (sub == null)
            {
                sub = new NetworkServiceSubscription
                {
                    NetworkId = companyNetworkId,
                    FeatureKey = MikroTikServersFeatureKey,
                    BillingPeriod = renewalBillingPeriod,
                    StartAt = now,
                    ExpiresAt = BillingPeriodDateCalculator.AddPeriod(now, renewalBillingPeriod),
                    Status = NetworkServiceSubscriptionStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now
                };

                _context.NetworkServiceSubscriptions.Add(sub);
                await _context.SaveChangesAsync(ct);
                await _usageChargeService.InitializeBaselineAsync(companyNetworkId, sub.Id, ct);
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

        private void ApplyClientImportOutcome(ClientImportOutcome outcome)
        {
            if (outcome.Success)
            {
                TempData["Success"] = $"✅ {outcome.SuccessMessage}";
                if (!string.IsNullOrEmpty(outcome.Warnings))
                {
                    TempData["Error"] = outcome.Warnings;
                }

                if (!string.IsNullOrEmpty(outcome.FailedUsersJson))
                {
                    TempData["ImportFailedUsersDetails"] = outcome.FailedUsersJson;
                }

                return;
            }

            string message = outcome.ErrorMessage ?? "فشل الاستيراد";
            TempData["Error"] = message.StartsWith('❌') ? message : $"❌ {message}";
        }
    }
}
