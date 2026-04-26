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
using RadTik.ViewModels.MikroTikServers;
using System.Threading.Tasks;

namespace RadTik.Controllers
{
    [Authorize(Roles = "NetworkAdministrator,CompanyEmployee,EmployeeLegacy")]
    [RequirePermission("MikroTikServers.View")]
    public class MikroTikServersController : Controller
    {
        private const string MikroTikServersFeatureKey = FeatureKeys.MikroTikServers;
        private readonly ApplicationDbContext _context;
        private readonly IMikroTikUsersService _mikroTikService;
        private readonly IMikroTikProfilesService _mikroTikProfilesService;
        private readonly ILogger<MikroTikServersController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;

        public MikroTikServersController(
            ApplicationDbContext context,
            IMikroTikUsersService mikroTikService,
            IMikroTikProfilesService mikroTikProfilesService,
            ILogger<MikroTikServersController> logger,
            UserManager<ApplicationUser> userManager,
            IUsageBasedSubscriptionChargeService usageChargeService)
        {
            _context = context;
            _mikroTikService = mikroTikService;
            _mikroTikProfilesService = mikroTikProfilesService;
            _logger = logger;
            _userManager = userManager;
            _usageChargeService = usageChargeService;
        }

        // GET: MikroTikServers
        public async Task<IActionResult> Index(int? selectedNetworkId)
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            var networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var accessibleNetworkIds = networks.Select(n => n.Id).ToHashSet();
            var showAllNetworks = selectedNetworkId.HasValue && selectedNetworkId.Value == 0;
            var effectiveNetworkId = selectedNetworkId.HasValue && selectedNetworkId.Value > 0
                ? selectedNetworkId.Value
                : networkId.Value;

            if (!showAllNetworks && !accessibleNetworkIds.Contains(effectiveNetworkId))
            {
                TempData["Error"] = "الشبكة المحددة غير متاحة لك.";
                return RedirectToAction(nameof(Index));
            }

            var serversQuery = _context.MikroTikServers
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

            var servers = await serversQuery.ToListAsync();

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
                var activeServersCount = servers.Count(s => s.IsActive);
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var mikrotikServer = await _context.MikroTikServers
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
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            // جلب قائمة الشبكات المتاحة للمستخدم
            var networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            var requiresExplicitNetworkSelection = networks.Count > 1;
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                if (!mikrotikServer.NetworkId.HasValue)
                {
                    ModelState.AddModelError("NetworkId", "يرجى تحديد الشبكة التي سيتم إضافة السيرفر لها.");
                }

                var selectedNetworkId = mikrotikServer.NetworkId ?? 0;
                if (mikrotikServer.NetworkId.HasValue)
                {
                    var hasAccess = await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, selectedNetworkId);
                    if (!hasAccess)
                    {
                        ModelState.AddModelError("NetworkId", "الشبكة المحددة غير متاحة لك.");
                    }
                }

                var (initialServerPricing, renewalServerPricing) = await GetServerPricingSettingsAsync();
                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;
                var scopeNetworkIds = await _context.Networks
                    .AsNoTracking()
                    .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
                    .Select(n => n.Id)
                    .ToListAsync();

                var currentServersCount = await _context.MikroTikServers
                    .AsNoTracking()
                    .CountAsync(s => s.IsActive && s.NetworkId.HasValue && scopeNetworkIds.Contains(s.NetworkId.Value));
                var isFirstServerFree = currentServersCount == 0;
                var oneTimeChargeAmount = initialServerPricing != null
                    ? WalletMath.CeilSyp(initialServerPricing.AmountSYP)
                    : 0m;

                if (!isFirstServerFree)
                {
                    if (initialServerPricing == null || initialServerPricing.BillingPeriod != PricingBillingPeriod.OneTime)
                    {
                        ModelState.AddModelError(string.Empty,
                            "لم يقم مدير النظام بتحديد سعر إنشاء السيرفرات الإضافية بعد. يرجى مراجعة تبويب الأسعار والتجديد.");
                    }

                    if (renewalServerPricing == null || renewalServerPricing.BillingPeriod == PricingBillingPeriod.OneTime)
                    {
                        ModelState.AddModelError(string.Empty,
                            "لم يقم مدير النظام بتحديد إعدادات تجديد اشتراك السيرفرات بعد. يرجى مراجعة تبويب الأسعار والتجديد.");
                    }
                }

                if (ModelState.IsValid)
                {
                    await using var tx = await _context.Database.BeginTransactionAsync();

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

                    // التحقق من عدم تكرار اسم الخادم في نفس الشبكة
                    var existingServer = await _context.MikroTikServers
                        .FirstOrDefaultAsync(s => s.NetworkId == selectedNetworkId && (s.Name == mikrotikServer.Name || s.Host == mikrotikServer.Host));

                    if (existingServer != null)
                    {
                        ModelState.AddModelError(string.Empty, "يوجد بالفعل خادم بهذا الاسم أو عنوان المضيف في شبكتك");
                        await tx.RollbackAsync();
                        await RebuildCreateViewStateAsync(user!, selectedNetworkId);
                        return View(mikrotikServer);
                    }

                    var now = DateTime.Now;
                    var companyNetwork = await _context.Networks
                        .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null);

                    if (companyNetwork == null)
                    {
                        ModelState.AddModelError(string.Empty, "تعذر تحديد حساب الشركة الرئيسي.");
                        await tx.RollbackAsync();
                        await RebuildCreateViewStateAsync(user!, selectedNetworkId);
                        return View(mikrotikServer);
                    }

                    if (!isFirstServerFree && oneTimeChargeAmount > 0 && companyNetwork.Balance < oneTimeChargeAmount)
                    {
                        ModelState.AddModelError(string.Empty,
                            $"الرصيد غير كافٍ لإضافة سيرفر إضافي. المطلوب: {oneTimeChargeAmount:N2} ل.س.ج، الرصيد الحالي: {companyNetwork.Balance:N2} ل.س.ج.");
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

                    if (!isFirstServerFree && oneTimeChargeAmount > 0)
                    {
                        var previousBalance = companyNetwork.Balance;
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

                    TempData["Success"] = "✅ تم إضافة الخادم بنجاح";
                    return RedirectToAction(nameof(Index));
                }
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "خطأ في حفظ بيانات الخادم");
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء حفظ البيانات. الرجاء المحاولة مرة أخرى.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطأ غير متوقع");
                ModelState.AddModelError(string.Empty, "حدث خطأ غير متوقع. الرجاء المحاولة مرة أخرى.");
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var currentNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var mikrotikServer = await _context.MikroTikServers
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            // التحقق من أن الخادم يتبع شبكة المستخدم (وجلب الكيان المُتتبَّع لتحديثه)
            var existingServer = await _context.MikroTikServers
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
                    existingServer.Name = mikrotikServer.Name;
                    existingServer.Host = mikrotikServer.Host;
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

                    TempData["Success"] = "✅ تم تحديث الخادم بنجاح";
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
                    ModelState.AddModelError(string.Empty, "حدث خطأ أثناء تحديث البيانات. تحقق من عدم تكرار اسم المضيف أو اسم الخادم.");
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var mikrotikServer = await _context.MikroTikServers
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (user == null || !networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                var mikrotikServer = await _context.MikroTikServers
                    .FirstOrDefaultAsync(m => m.Id == id && m.NetworkId == networkId.Value);
                if (mikrotikServer != null)
                {
                    // التحقق من وجود قطاعات مرتبطة بهذا الخادم
                    var hasSectors = await _context.Sectors.AnyAsync(s => s.MikroTikServerId == id);

                    if (hasSectors)
                    {
                        TempData["Error"] = "❌ لا يمكن حذف هذا الخادم لأنه مرتبط بقطاعات. الرجاء إزالة القطاعات أولاً.";
                        return RedirectToAction(nameof(Index));
                    }

                    _context.MikroTikServers.Remove(mikrotikServer);
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "✅ تم حذف الخادم بنجاح";
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (user == null || !networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            
            if (server == null)
            {
                return NotFound();
            }

            try
            {
                var activeUsers = await _mikroTikService.GetActivePPPoEUsers(id.Value);
                ViewData["ServerName"] = server.Host;
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            
            if (server == null)
            {
                return NotFound();
            }

            try
            {
                var allUsers = await _mikroTikService.GetAllUsersWithDetails(id.Value);
                ViewData["ServerName"] = server.Host;
                ViewData["ServerId"] = id.Value;

                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
                var preview = await _mikroTikService.BuildUsersImportPreviewAsync(id.Value, networkId.Value);
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
                var baseSubscriberUnitEstimate = await _usageChargeService.EstimateImportChargeAsync(
                    companyNetworkId,
                    PricingChargeUnit.PerSubscriber,
                    1);
                ViewData["ImportPreview"] = preview;
                ViewData["ImportEstimate"] = estimate;
                ViewData["ClientImportUnitPrice"] = baseSubscriberUnitEstimate.UnitPriceSyp;
                
                // تصفية المستقبلات حسب الشبكة
                var receivers = await _context.Receivers
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
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            // التأكد من أن الخادم يتبع نفس الشبكة
            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

            if (server == null)
            {
                return NotFound();
            }

            try
            {
                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                var preview = await _mikroTikService.BuildUsersImportPreviewAsync(serverId, networkId.Value);
                if (preview.MissingProfileCount > 0)
                {
                    TempData["Error"] = $"لا يمكن استيراد المشتركين قبل استيراد البروفايلات. يوجد {preview.MissingProfileCount} مشترك مرتبط ببروفايلات غير مستوردة.";
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
                }

                if (preview.ImportableUsersCount <= 0)
                {
                    TempData["Error"] = "لا يوجد عملاء جدد قابلين للاستيراد من هذا السيرفر حالياً.";
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
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
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
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
                    if (result.FailedCount > 0)
                    {
                        TempData["Error"] = string.Join(" | ", result.Errors.Take(5));
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            
            if (server == null)
            {
                TempData["Error"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                // جلب بيانات المستخدم من MikroTik
                var allUsers = await _mikroTikService.GetAllUsersWithDetails(id.Value);
                var mikrotikUser = allUsers.FirstOrDefault(u => u.UserName == userName);

                if (mikrotikUser == null)
                {
                    TempData["Error"] = "المستخدم غير موجود";
                    return RedirectToAction(nameof(AllUsers), new { id = id });
                }

                // جلب البروفايلات من السيرفر الحالي
                var profiles = await _mikroTikProfilesService.GetProfileNamesFromMikroTik(id.Value);

                // تحويل القائمة إلى SelectListItem
                var profileItems = profiles.Select(p => new SelectListItem
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

                ViewBag.ServerName = server.Host;
                ViewBag.ServerId = id.Value;
                
                // تصفية المستقبلات حسب الشبكة
                var receivers = await _context.Receivers
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
                var errors = ModelState
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
                    var profiles = await _mikroTikProfilesService.GetProfileNamesFromMikroTik(id);
                    if (!profiles.Contains(model.ProfileName))
                    {
                        ModelState.AddModelError("ProfileName", "البروفايل المحدد غير موجود في السيرفر");
                        await ReloadViewBagData(id, model);
                        TempData["Error"] = "البروفايل المحدد غير موجود في السيرفر";
                        return View(model);
                    }
                }

                // تحديث بيانات المستخدم
                var result = await _mikroTikService.UpdateUserFromAllUsers(model);

                if (result)
                {
                    TempData["Success"] = "✅ تم تحديث بيانات العميل بنجاح";
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
            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);

            if (networkId.HasValue)
            {
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);
                
                if (server != null)
                {
                    ViewBag.ServerName = server.Host;
                }
            }

            ViewBag.ServerId = serverId;
            
            // تصفية المستقبلات حسب الشبكة
            if (networkId.HasValue)
            {
                var receivers = await _context.Receivers
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
                var profiles = await _mikroTikProfilesService.GetProfileNamesFromMikroTik(serverId);
                var profileItems = profiles.Select(p => new SelectListItem
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
                .FirstOrDefaultAsync(s => s.Id == id && s.NetworkId == networkId.Value);
            
            if (server == null)
            {
                return NotFound();
            }

            try
            {
                var isConnected = await _mikroTikService.TestConnection(id.Value);
                if (isConnected)
                {
                    TempData["Success"] = "✓ الاتصال ناجح بالخادم";
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

            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (!networkId.HasValue)
            {
                TempData["Error"] = "يرجى تحديد شبكة أولاً";
                return RedirectToAction("Index", "Network");
            }

            var server = await _context.MikroTikServers
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["Error"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                await _mikroTikService.FreezeAccount(serverId, userName);

                // تحديث قاعدة البيانات إذا كان المستخدم موجودًا فيها (في نفس الشبكة)
                var client = await _context.Clients
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction(nameof(AllUsers), new { id = serverId });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["Error"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                await _mikroTikService.UnfreezeAccount(serverId, userName);

                // تحديث قاعدة البيانات إذا كان المستخدم موجودًا فيها (في نفس الشبكة)
                var client = await _context.Clients
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
            var rawMessage = ex.Message ?? string.Empty;
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
            var networks = await NetworkHelper.GetAvailableNetworksAsync(_context, user, _userManager);
            var requiresExplicitNetworkSelection = networks.Count > 1;
            ViewBag.Networks = new SelectList(networks, "Id", "Name", networkId);
            ViewBag.CurrentNetworkId = networkId;
            ViewBag.RequiresExplicitNetworkSelection = requiresExplicitNetworkSelection;
            await PopulateServerPricingPreviewAsync(user, networkId);
        }

        private async Task PopulateServerRenewalSummaryAsync(int selectedNetworkId)
        {
            var selected = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);
            var companyNetworkId = selected?.ParentNetworkId ?? selectedNetworkId;

            var scopeNetworkIds = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
                .Select(n => n.Id)
                .ToListAsync();

            var activeServersCount = await _context.MikroTikServers
                .AsNoTracking()
                .CountAsync(s => s.IsActive && s.NetworkId.HasValue && scopeNetworkIds.Contains(s.NetworkId.Value));

            var additionalServersCount = Math.Max(0, activeServersCount - 1);
            var (_, renewalServerPricing) = await GetServerPricingSettingsAsync();
            var renewalUnitPrice = renewalServerPricing != null ? WalletMath.CeilSyp(renewalServerPricing.AmountSYP) : 0m;

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
            var selected = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == selectedNetworkId);

            var companyNetworkId = selected?.ParentNetworkId ?? selectedNetworkId;
            var company = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == companyNetworkId);

            var scopeNetworkIds = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId || n.ParentNetworkId == companyNetworkId)
                .Select(n => n.Id)
                .ToListAsync();

            var serversCount = await _context.MikroTikServers
                .AsNoTracking()
                .CountAsync(s => s.IsActive && s.NetworkId.HasValue && scopeNetworkIds.Contains(s.NetworkId.Value));

            var isFirstServerFree = serversCount == 0;
            var (initialServerPricing, renewalServerPricing) = await GetServerPricingSettingsAsync();

            ViewBag.ServerPricingIsFirstFree = isFirstServerFree;
            ViewBag.ServerPricingCompanyName = company?.Name ?? selected?.Name ?? "غير محدد";
            ViewBag.ServerPricingTotalServers = serversCount;
            ViewBag.ServerPricingHasInitial = initialServerPricing != null;
            ViewBag.ServerPricingHasRenewal = renewalServerPricing != null;
            ViewBag.ServerPricingInitialSyp = initialServerPricing != null ? WalletMath.CeilSyp(initialServerPricing.AmountSYP) : 0m;
            ViewBag.ServerPricingRenewalSyp = renewalServerPricing != null ? WalletMath.CeilSyp(renewalServerPricing.AmountSYP) : 0m;
            ViewBag.ServerPricingRenewalPeriodLabel = renewalServerPricing != null
                ? PricingDisplay.BillingPeriodLabel(renewalServerPricing.BillingPeriod)
                : "غير محدد";
        }

        private async Task<(FeaturePricing? Initial, FeaturePricing? Renewal)> GetServerPricingSettingsAsync()
        {
            var pricingRows = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == MikroTikServersFeatureKey &&
                    p.ChargeUnit == PricingChargeUnit.PerServer)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            var initial = pricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
            var renewal = pricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
            return (initial, renewal);
        }

        private async Task EnsureServerSubscriptionAsync(
            int companyNetworkId,
            PricingBillingPeriod renewalBillingPeriod,
            DateTime now,
            CancellationToken ct)
        {
            var sub = await _context.NetworkServiceSubscriptions
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

    }
}