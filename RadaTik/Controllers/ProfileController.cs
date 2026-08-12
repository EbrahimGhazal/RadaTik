using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.PricingPreview;
using RadaTik.Domain.Profiles;
using RadaTik.Services.Profiles;
using RadaTik.Helpers;
using RadaTik.Security;
using RadaTik.ViewModels.Profile;
using RadaTik.Dtos.MikroTik;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;

namespace RadaTik.Controllers
{
    [Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
    public partial class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMikroTikProfilesService _mikroTikService;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ICreatePricingPreviewService _pricingPreviewService;
        private readonly ICompanyProfileCatalogService _catalogService;
        private readonly IProfileImportPricingService _profileImportPricing;
        private readonly IProfileListQueryService _profileListQuery;
        private readonly IProfileImportPreviewService _profileImportPreview;
        private readonly IProfileFormViewDataService _profileFormViewData;
        private readonly IProfileCompanyWalletService _profileCompanyWallet;
        private readonly IProfileMikroTikSyncOrchestrator _profileMikroTikSync;

        public ProfileController(
            ApplicationDbContext context,
            IMikroTikProfilesService mikroTikService,
            ILogger<ProfileController> logger,
            UserManager<ApplicationUser> userManager,
            ICreatePricingPreviewService pricingPreviewService,
            ICompanyProfileCatalogService catalogService,
            IProfileImportPricingService profileImportPricing,
            IProfileListQueryService profileListQuery,
            IProfileImportPreviewService profileImportPreview,
            IProfileFormViewDataService profileFormViewData,
            IProfileCompanyWalletService profileCompanyWallet,
            IProfileMikroTikSyncOrchestrator profileMikroTikSync)
        {
            _context = context;
            _mikroTikService = mikroTikService;
            _logger = logger;
            _userManager = userManager;
            _pricingPreviewService = pricingPreviewService;
            _catalogService = catalogService;
            _profileImportPricing = profileImportPricing;
            _profileListQuery = profileListQuery;
            _profileImportPreview = profileImportPreview;
            _profileFormViewData = profileFormViewData;
            _profileCompanyWallet = profileCompanyWallet;
            _profileMikroTikSync = profileMikroTikSync;
        }

        // GET: Profile/Index
        public async Task<IActionResult> Index(int? serverId = null)
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

                ProfileIndexPageModel? page = await _profileListQuery.BuildIndexPageAsync(networkId.Value, serverId);
                if (page == null)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction("Index", "Network");
                }

                ViewBag.MikroTikServers = page.Servers;
                ViewBag.SelectedServerId = page.SelectedServerId;
                ViewBag.ProfileImportUnitPrice = page.ProfileImportUnitPrice;
                ViewBag.CompanyCatalogs = page.CompanyCatalogs;
                ViewBag.TotalProfiles = page.TotalProfiles;
                ViewBag.ActiveProfiles = page.ActiveProfiles;
                ViewBag.SyncedProfiles = page.SyncedProfiles;

                return View(page.Profiles.ToList());
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب قائمة البروفايلات");
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("خطأ في جلب البيانات", ex);
                return View(new List<Profile>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetImportPreviewData(int serverId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً." });
                }

                ProfileImportPreviewJsonModel preview =
                    await _profileImportPreview.BuildImportPreviewJsonAsync(serverId, networkId.Value);
                if (!preview.Success)
                {
                    return Json(new { success = false, message = preview.Message });
                }

                return Json(new
                {
                    success = true,
                    serverId = preview.ServerId,
                    totalProfiles = preview.TotalProfiles,
                    importableProfiles = preview.ImportableProfiles,
                    unitPrice = preview.UnitPrice,
                    totalCharge = preview.TotalCharge,
                    walletBalance = preview.WalletBalance
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذر جلب معاينة استيراد البروفايلات");
                return Json(new
                {
                    success = false,
                    message = MikroTikProfileErrorFormatter.Format("تعذر جلب معاينة الاستيراد", ex)
                });
            }
        }

        // GET: Profile/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "لم يتم تحديد بروفايل";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction("Index", "Network");
                }

                Profile? profile = await _context.Profiles
                    .Where(p => p.NetworkId == networkId.Value)
                    .Include(p => p.MikroTikServer)
                    .Include(p => p.Clients)
                    .Include(p => p.ProfilePriceHistories)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // جلب معلومات إضافية من MikroTik إذا كان البروفايل متزامناً
                if (profile.IsSyncedWithMikroTik && profile.MikroTikServerId > 0)
                {
                    try
                    {
                        MikroTikProfileInfo mtInfo = await _mikroTikService.GetProfileFromMikroTik(
                            profile.MikroTikServerId,
                            profile.MikroTikProfileId ?? profile.Name);

                        ViewBag.MikroTikInfo = mtInfo;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ تعذر جلب معلومات البروفايل من MikroTik: {ErrorMessage}", ex.Message);
                        ViewBag.MikroTikError = MikroTikProfileErrorFormatter.Format("تعذر جلب معلومات البروفايل من MikroTik", ex);
                    }
                }

                return View(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في عرض تفاصيل البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("خطأ في عرض التفاصيل", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Profile/Create
        public async Task<IActionResult> Create(int? serverId = null)
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

                if (serverId is > 0)
                {
                    ViewBag.SelectedDeployServerIds = new[] { serverId.Value };
                }

                await LoadCreateViewData();

                // إضافة قيم افتراضية لـ ViewData
                ViewData["DefaultDownloadSpeed"] = 10;
                ViewData["DefaultUploadSpeed"] = 10;
                ViewData["DefaultPrice"] = 100;

                // إضافة قيم Enum لـ ViewData
                ViewData["ProfileTypes"] = Enum.GetValues<ProfileType>()
                    .Select(e => new
                    {
                        Value = e,
                        Text = e.ToString(),
                        Description = GetEnumDescription(e)
                    })
                    .ToList();

                ViewData["BillingCycles"] = Enum.GetValues<BillingCycle>()
                    .Select(e => new
                    {
                        Value = e,
                        Text = e.ToString(),
                        Description = GetEnumDescription(e)
                    })
                    .ToList();

                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحميل صفحة إنشاء بروفايل");
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("خطأ في تحميل الصفحة", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Profile/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Profile profile, int[]? deployToServerIds)
        {
            try
            {
                int[] serverIds = deployToServerIds?.Where(id => id > 0).Distinct().ToArray() ?? Array.Empty<int>();
                if (serverIds.Length > 0)
                {
                    profile.MikroTikServerId = serverIds[0];
                }

                if (!ModelState.IsValid)
                {
                    await LoadCreateViewData();
                    ViewBag.SelectedDeployServerIds = serverIds;
                    return View(profile);
                }

                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    await LoadCreateViewData();
                    return View(profile);
                }

                if (serverIds.Length == 0)
                {
                    ModelState.AddModelError(string.Empty, "اختر سيرفراً واحداً على الأقل لنشر البروفايل.");
                    await LoadCreateViewData();
                    ViewBag.SelectedDeployServerIds = serverIds;
                    return View(profile);
                }

                profile.VATPercentage = await _profileCompanyWallet.ResolveSystemProfileVatPercentageAsync();

                Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
                ProfileImportChargeEstimate createCharge =
                    await _profileImportPricing.CalculateProfileChargeAsync(companyNetworkId, serverIds.Length);
                if (!createCharge.HasSufficientBalance)
                {
                    ModelState.AddModelError(string.Empty,
                        $"لا يمكن إضافة البروفايل حالياً: الرصيد الحالي ({createCharge.WalletBalance:N2}) أقل من المبلغ المطلوب ({createCharge.TotalCharge:N2}) ل.س.ج ({serverIds.Length} سيرفر).");
                    await LoadCreateViewData();
                    ViewBag.SelectedDeployServerIds = serverIds;
                    return View(profile);
                }

                CompanyProfileCatalogService.CatalogOperationResult result =
                    await _catalogService.CreateCatalogAndDeployAsync(profile, serverIds, networkId.Value);

                if (!result.Success)
                {
                    if (result.CatalogId.HasValue)
                    {
                        TempData["InfoMessage"] = result.ErrorMessage;
                        return RedirectToAction(nameof(Deploy), new { id = result.CatalogId.Value });
                    }

                    ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "فشل إنشاء البروفايل.");
                    await LoadCreateViewData();
                    ViewBag.SelectedDeployServerIds = serverIds;
                    return View(profile);
                }

                decimal chargedAmount = await _profileCompanyWallet.ChargeCompanyForProfileUnitsAsync(
                    companyNetworkId,
                    user!.Id,
                    result.DeployedCount,
                    $"خصم إضافة بروفايل «{profile.Name}» على {result.DeployedCount} سيرفر");

                TempData["SuccessMessage"] = chargedAmount > 0m
                    ? $"تم إنشاء بروفايل الشركة «{profile.Name}» ونشره على {result.DeployedCount} سيرفر، وتم خصم {chargedAmount:N2} ل.س.ج."
                    : $"تم إنشاء بروفايل الشركة «{profile.Name}» ونشره على {result.DeployedCount} سيرفر.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في إنشاء البروفايل: {ErrorMessage}", ex.Message);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشل إنشاء البروفايل", ex);

                await LoadCreateViewData();
                return View(profile);
            }
        }

        [HttpGet]
        public async Task<IActionResult> Deploy(int id)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Index));
            }

            Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            CompanyProfileCatalog? catalog = await _context.CompanyProfileCatalogs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id && c.CompanyNetworkId == companyNetworkId);
            if (catalog == null)
            {
                return NotFound();
            }

            HashSet<int> deployedServerIds = await _context.Profiles
                .AsNoTracking()
                .Where(p => p.CompanyProfileCatalogId == id)
                .Select(p => p.MikroTikServerId)
                .ToHashSetAsync();

            List<MikroTikServer> servers = await _catalogService.GetDeployableServersAsync(networkId.Value, id);
            ProfileCatalogDeployViewModel vm = new()
            {
                CatalogId = catalog.Id,
                CatalogName = catalog.Name,
                Servers = servers.Select(s => new ServerDeployOption
                {
                    ServerId = s.Id,
                    ServerName = s.Name ?? $"#{s.Id}",
                    Host = s.Host,
                    AlreadyDeployed = deployedServerIds.Contains(s.Id)
                }).ToList()
            };

            ViewData["Title"] = $"نشر البروفايل — {catalog.Name}";
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deploy(int id, int[]? deployToServerIds)
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            if (!networkId.HasValue || user == null)
            {
                TempData["Error"] = AppMessages.SelectNetworkFirst;
                return RedirectToAction(nameof(Index));
            }

            int[] serverIds = deployToServerIds?.Where(s => s > 0).Distinct().ToArray() ?? Array.Empty<int>();
            if (serverIds.Length == 0)
            {
                TempData["Error"] = "اختر سيرفراً واحداً على الأقل.";
                return RedirectToAction(nameof(Deploy), new { id });
            }

            Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
            int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

            ProfileImportChargeEstimate charge =
                await _profileImportPricing.CalculateProfileChargeAsync(companyNetworkId, serverIds.Length);
            if (!charge.HasSufficientBalance)
            {
                TempData["Error"] =
                    $"الرصيد غير كافٍ. المطلوب {charge.TotalCharge:N2} ل.س.ج والمتاح {charge.WalletBalance:N2} ل.س.ج.";
                return RedirectToAction(nameof(Deploy), new { id });
            }

            CompanyProfileCatalog? catalog = await _context.CompanyProfileCatalogs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == id);
            CompanyProfileCatalogService.CatalogOperationResult result =
                await _catalogService.DeployCatalogToServersAsync(id, serverIds, networkId.Value);

            if (!result.Success)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Deploy), new { id });
            }

            decimal chargedAmount = await _profileCompanyWallet.ChargeCompanyForProfileUnitsAsync(
                companyNetworkId,
                user.Id,
                result.DeployedCount,
                $"خصم نشر بروفايل «{catalog?.Name ?? id.ToString()}» على {result.DeployedCount} سيرفر");

            string message = $"تم النشر على {result.DeployedCount} سيرفر جديد.";
            if (chargedAmount > 0m)
            {
                message += $" تم خصم {chargedAmount:N2} ل.س.ج.";
            }

            if (result.Warnings.Count > 0)
            {
                message += " " + string.Join(" ", result.Warnings);
            }

            TempData["SuccessMessage"] = message;
            return RedirectToAction(nameof(Index));
        }

        // GET: Profile/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "لم يتم تحديد بروفايل";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction("Index", "Network");
                }

                Profile? profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == id && p.NetworkId == networkId.Value);
                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                await LoadEditViewData();

                // إضافة قيم Enum
                ViewData["ProfileTypes"] = Enum.GetValues<ProfileType>()
                    .Select(e => new
                    {
                        Value = e,
                        Text = e.ToString(),
                        Description = GetEnumDescription(e)
                    })
                    .ToList();

                ViewData["BillingCycles"] = Enum.GetValues<BillingCycle>()
                    .Select(e => new
                    {
                        Value = e,
                        Text = e.ToString(),
                        Description = GetEnumDescription(e)
                    })
                    .ToList();

                return View(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحميل صفحة تعديل البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("خطأ في تحميل الصفحة", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Profile/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Profile profile)
        {
            if (id != profile.Id)
            {
                TempData["ErrorMessage"] = "معرف البروفايل غير متطابق";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                if (!ModelState.IsValid)
                {
                    await LoadEditViewData();
                    return View(profile);
                }

                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    await LoadEditViewData();
                    return View(profile);
                }

                Profile? existingProfile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == id && p.NetworkId == networkId.Value);

                if (existingProfile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // التحقق من تغيير السعر
                if (existingProfile.Price != profile.Price)
                {
                    decimal systemVatPercentage = await _profileCompanyWallet.ResolveSystemProfileVatPercentageAsync();
                    ProfilePriceHistory priceHistory = new ProfilePriceHistory
                    {
                        ProfileId = profile.Id,
                        OldPrice = existingProfile.Price,
                        NewPrice = profile.Price,
                        OldVATPercentage = existingProfile.VATPercentage,
                        NewVATPercentage = systemVatPercentage,
                        ChangeReason = "تعديل السعر من لوحة التحكم",
                        ChangeDate = DateTime.Now,
                        ChangedBy = User.Identity?.Name ?? "System"
                    };
                    _context.ProfilePriceHistories.Add(priceHistory);
                }

                // التحقق من تغيير الاسم إذا كان البروفايل متزامناً
                string? oldName = null;
                if (existingProfile.Name != profile.Name && existingProfile.IsSyncedWithMikroTik)
                {
                    oldName = existingProfile.Name;
                }

                // تحديث في MikroTik إذا كان البروفايل متزامناً
                if (profile.IsSyncedWithMikroTik && profile.MikroTikServerId > 0)
                {
                    try
                    {
                        _logger.LogInformation("🔄 محاولة تحديث البروفايل {ProfileName} في MikroTik...", profile.Name);

                        await _mikroTikService.UpdateProfileInMikroTik(
                            profile.MikroTikServerId,
                            profile,
                            oldName);

                        profile.LastSyncDate = DateTime.Now;
                        _logger.LogInformation("✅ تم تحديث البروفايل {ProfileName} في MikroTik بنجاح", profile.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ فشل تحديث البروفايل في MikroTik: {ErrorMessage}", ex.Message);
                        profile.IsSyncedWithMikroTik = false;
                        TempData["WarningMessage"] = MikroTikProfileErrorFormatter.Format("تم تحديث البروفايل في قاعدة البيانات ولكن فشل تحديثه في MikroTik", ex);
                    }
                }

                profile.UpdatedDate = DateTime.Now;
                profile.NetworkId = networkId.Value;
                profile.VATPercentage = await _profileCompanyWallet.ResolveSystemProfileVatPercentageAsync();

                DateTime? previousLastSync = existingProfile.LastSyncDate;
                _context.Entry(existingProfile).CurrentValues.SetValues(profile);
                existingProfile.NetworkId = networkId.Value;
                if (!profile.LastSyncDate.HasValue)
                {
                    existingProfile.LastSyncDate = previousLastSync;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = AppMessages.OperationSuccess;
                return RedirectToAction(nameof(Index), new { serverId = existingProfile.MikroTikServerId });
            }
            catch (DbUpdateConcurrencyException ex)
            {
                if (!ProfileExists(profile.Id))
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }
                else
                {
                    _logger.LogError(ex, "❌ خطأ في تحديث البروفايل {ProfileId}", id);
                    TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("خطأ في التحديث", ex);
                    await LoadEditViewData();
                    return View(profile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحديث البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشل تحديث البروفايل", ex);
                await LoadEditViewData();
                return View(profile);
            }
        }

        // GET: Profile/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                TempData["ErrorMessage"] = "لم يتم تحديد بروفايل";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = AppMessages.SelectNetworkFirst;
                    return RedirectToAction("Index", "Network");
                }

                Profile? profile = await _context.Profiles
                    .Where(p => p.NetworkId == networkId.Value)
                    .Include(p => p.MikroTikServer)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // التحقق من وجود عملاء مرتبطين في نفس الشبكة
                int clientsCount = await _context.Clients
                    .CountAsync(c => c.ProfileId == id && c.NetworkId == networkId.Value);
                ViewData["ClientsCount"] = clientsCount;

                if (clientsCount > 0)
                {
                    ViewBag.ErrorMessage = $"لا يمكن حذف هذا البروفايل لأنه مرتبط بـ {clientsCount} عميل.";
                    ViewBag.ClientsList = await _context.Clients
                        .Where(c => c.ProfileId == id && c.NetworkId == networkId.Value)
                        .Take(10)
                        .Select(c => c.Name)
                        .ToListAsync();
                }

                return View(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحميل صفحة حذف البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("خطأ في تحميل الصفحة", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Profile/DeleteConfirmed
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
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

                Profile? profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == id && p.NetworkId == networkId.Value);
                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // التحقق من وجود عملاء مرتبطين في نفس الشبكة
                int clientsCount = await _context.Clients
                    .CountAsync(c => c.ProfileId == id && c.NetworkId == networkId.Value);
                if (clientsCount > 0)
                {
                    TempData["ErrorMessage"] = $"لا يمكن حذف هذا البروفايل لأنه مرتبط بـ {clientsCount} عميل.";
                    return RedirectToAction(nameof(Delete), new { id });
                }

                // حذف من MikroTik إذا كان البروفايل متزامناً
                if (profile.IsSyncedWithMikroTik && profile.MikroTikServerId > 0)
                {
                    try
                    {
                        await _mikroTikService.DeleteProfileFromMikroTik(
                            profile.MikroTikServerId,
                            profile.Name);

                        _logger.LogInformation("✅ تم حذف البروفايل {ProfileName} من MikroTik بنجاح", profile.Name);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ فشل حذف البروفايل من MikroTik: {ErrorMessage}", ex.Message);
                        TempData["WarningMessage"] = MikroTikProfileErrorFormatter.Format("تم حذف البروفايل من قاعدة البيانات ولكن حدث خطأ في حذفه من MikroTik", ex);
                    }
                }

                // حذف من قاعدة البيانات
                _context.Profiles.Remove(profile);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = AppMessages.OperationSuccess;
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في حذف البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشل حذف البروفايل", ex);
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        // ===== دوال المزامنة =====

        // GET: Profile/ViewMikroTikProfiles
        public async Task<IActionResult> ViewMikroTikProfiles(int serverId)
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

                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                List<MikroTikProfileInfo> profiles = await _mikroTikService.GetProfilesFromMikroTik(serverId);
                ViewBag.MikroTikServer = server;
                ViewBag.AllServers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .ToListAsync();

                return View(profiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب البروفايلات من MikroTik للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشل جلب البروفايلات من MikroTik", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Profile/SyncFromMikroTik
        public async Task<IActionResult> SyncFromMikroTik(int serverId, bool importAsInactive = false, decimal defaultPrice = 100m)
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

                ProfileSyncFromMikroTikOutcome outcome = await _profileMikroTikSync.SyncFromMikroTikAsync(new ProfileSyncFromMikroTikCommand
                {
                    ServerId = serverId,
                    NetworkId = networkId.Value,
                    ActorUserId = user!.Id,
                    ImportAsInactive = importAsInactive,
                    DefaultPrice = defaultPrice
                });

                switch (outcome.Status)
                {
                    case ProfileSyncFromMikroTikStatus.Success:
                        TempData["SuccessMessage"] = outcome.Message;
                        if (outcome.SyncResult != null)
                        {
                            ApplyProfileSyncFailureWarning(outcome.SyncResult);
                        }
                        break;
                    case ProfileSyncFromMikroTikStatus.Info:
                    case ProfileSyncFromMikroTikStatus.NoImportable:
                        TempData["InfoMessage"] = outcome.Message;
                        break;
                    default:
                        TempData["ErrorMessage"] = outcome.Message;
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في مزامنة البروفايلات من MikroTik للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشلت المزامنة", ex);
            }

            return RedirectToAction(nameof(Index), new { serverId });
        }

        // GET: Profile/SyncFromMikroTikJson — استيراد خادم واحد مع نتيجة JSON لتقدم الواجهة
        [HttpGet]
        public async Task<IActionResult> SyncFromMikroTikJson(int serverId, bool importAsInactive = false, decimal defaultPrice = 100m)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, status = "NetworkRequired", message = AppMessages.SelectNetworkFirst });
                }

                MikroTikServer? server = await _context.MikroTikServers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == serverId);
                string serverLabel = server?.Name ?? $"#{serverId}";
                int syncNetworkId = server?.NetworkId ?? networkId.Value;

                if (server == null ||
                    !server.NetworkId.HasValue ||
                    !await NetworkHelper.IsNetworkAccessibleAsync(HttpContext, _context, user, syncNetworkId))
                {
                    return Json(new
                    {
                        success = false,
                        status = nameof(ProfileSyncFromMikroTikStatus.ServerNotFound),
                        serverId,
                        serverName = serverLabel,
                        message = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه"
                    });
                }

                // نستخدم NetworkId الخاص بالخادم (وليس فقط شبكة الجلسة) حتى يعمل الاستيراد
                // عندما تكون الخوادم على شبكة فرعية ضمن نطاق صلاحية المدير.
                ProfileSyncFromMikroTikOutcome outcome = await _profileMikroTikSync.SyncFromMikroTikAsync(new ProfileSyncFromMikroTikCommand
                {
                    ServerId = serverId,
                    NetworkId = syncNetworkId,
                    ActorUserId = user!.Id,
                    ImportAsInactive = importAsInactive,
                    DefaultPrice = defaultPrice
                });

                bool ok = outcome.Status is ProfileSyncFromMikroTikStatus.Success
                    or ProfileSyncFromMikroTikStatus.Info
                    or ProfileSyncFromMikroTikStatus.NoImportable;

                return Json(new
                {
                    success = ok,
                    status = outcome.Status.ToString(),
                    serverId,
                    serverName = serverLabel,
                    message = outcome.Message,
                    chargedAmount = outcome.ChargedAmount
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في مزامنة البروفايلات من MikroTik (JSON) للخادم {ServerId}", serverId);
                return Json(new
                {
                    success = false,
                    status = "Error",
                    serverId,
                    message = MikroTikProfileErrorFormatter.Format("فشلت المزامنة", ex)
                });
            }
        }

        // GET: Profile/SyncFromMikroTikMany
        // لا ننفّذ الاستيراد هنا (طلب طويل يسبب مهلة/صفحة بيضاء لعدة خوادم).
        // نعيد التوجيه لصفحة Index لتشغيل الاستيراد تدريجياً عبر AJAX مع شريط تقدّم.
        [HttpGet]
        public IActionResult SyncFromMikroTikMany(int[] serverIds)
        {
            int[] ids = (serverIds ?? Array.Empty<int>()).Where(id => id > 0).Distinct().ToArray();
            if (ids.Length == 0)
            {
                TempData["ErrorMessage"] = "الرجاء اختيار خادم MikroTik واحد على الأقل.";
                return RedirectToAction(nameof(Index));
            }

            return RedirectToAction(nameof(Index), new { importServerIds = string.Join(",", ids) });
        }

        // GET: Profile/SyncToMikroTik
        public async Task<IActionResult> SyncToMikroTik(int serverId)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                SyncResult result = await _mikroTikService.SyncFromDatabaseToMikroTik(serverId, networkId.Value);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    ApplyProfileSyncFailureWarning(result);
                }
                else
                {
                    TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Sanitize(result.Message, "فشلت المزامنة");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في مزامنة البروفايلات إلى MikroTik للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشلت المزامنة", ex);
            }

            return RedirectToAction(nameof(Index), new { serverId });
        }

        // GET: Profile/TwoWaySync
        public async Task<IActionResult> TwoWaySync(int serverId, decimal defaultImportPrice = 100m)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                if (defaultImportPrice < 0m)
                {
                    defaultImportPrice = 0m;
                }

                if (defaultImportPrice > 1_000_000m)
                {
                    defaultImportPrice = 1_000_000m;
                }

                SyncResult result = await _mikroTikService.TwoWaySync(serverId, networkId.Value, defaultImportPrice);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    ApplyProfileSyncFailureWarning(result);
                }
                else
                {
                    TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Sanitize(result.Message, "فشلت المزامنة");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في المزامنة الثنائية للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشلت المزامنة", ex);
            }

            return RedirectToAction(nameof(Index), new { serverId });
        }

        // POST: Profile/ImportFromMikroTik
        [HttpPost]
        public async Task<IActionResult> ImportFromMikroTik(ImportProfileViewModel model)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == model.MikroTikServerId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                if (model.SelectedProfileIds == null || model.SelectedProfileIds.Count == 0)
                {
                    TempData["ErrorMessage"] = "لم تقم باختيار أي بروفايل للاستيراد";
                    return RedirectToAction(nameof(ViewMikroTikProfiles), new { serverId = model.MikroTikServerId });
                }

                Network? selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                int companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                ImportProfilesPreviewResult importPreview = await _mikroTikService.BuildProfilesImportPreviewAsync(model.MikroTikServerId, networkId.Value);
                if (importPreview.ImportableProfilesCount <= 0)
                {
                    TempData["InfoMessage"] = "لا يوجد بروفايلات جديدة للاستيراد من هذا السيرفر. إذا أردت تحديث البروفايلات الحالية استخدم «المزامنة الثنائية».";
                    return RedirectToAction(nameof(Index));
                }

                List<MikroTikProfileInfo> mikrotikProfiles = await _mikroTikService.GetProfilesFromMikroTik(model.MikroTikServerId);
                HashSet<string> existingNames = (await _context.Profiles
                    .AsNoTracking()
                    .Where(p => p.MikroTikServerId == model.MikroTikServerId && p.NetworkId == networkId.Value && !string.IsNullOrEmpty(p.Name))
                    .Select(p => p.Name)
                    .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                int selectedImportableCount = mikrotikProfiles
                    .Where(p => model.SelectedProfileIds.Contains(p.Id) && !existingNames.Contains(p.Name))
                    .Count();

                ProfileImportChargeEstimate importCharge = await _profileImportPricing.CalculateProfileChargeAsync(companyNetworkId, selectedImportableCount);
                if (!importCharge.HasSufficientBalance)
                {
                    TempData["ErrorMessage"] =
                        $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({importCharge.WalletBalance:N2}) أقل من المبلغ المطلوب ({importCharge.TotalCharge:N2}) ل.س.ج.";
                    return RedirectToAction(nameof(Index));
                }

                int importedCount = 0;
                List<string> failedProfiles = new List<string>();

                foreach (string profileId in model.SelectedProfileIds)
                {
                    try
                    {
                        MikroTikProfileInfo? mtProfile = mikrotikProfiles.FirstOrDefault(p => p.Id == profileId);
                        if (mtProfile != null)
                        {
                            // التحقق من عدم وجود البروفايل مسبقاً في نفس الشبكة
                            Profile? existingProfile = await _context.Profiles
                                .FirstOrDefaultAsync(p => p.Name == mtProfile.Name && p.MikroTikServerId == model.MikroTikServerId && p.NetworkId == networkId.Value);

                            if (existingProfile == null)
                            {
                                Profile newProfile = new Profile
                                {
                                    Name = mtProfile.Name,
                                    Description = $"مستورد من MikroTik - {DateTime.Now:yyyy-MM-dd}",
                                    Type = GetProfileTypeFromService(mtProfile.Service),
                                    BillingCycle = BillingCycle.Monthly,
                                    Price = model.SetDefaultPrice ? model.DefaultPrice : 0,
                                    VATPercentage = await _profileCompanyWallet.ResolveSystemProfileVatPercentageAsync(),
                                    DownloadSpeed = 10, // قيمة افتراضية
                                    DownloadSpeedUnit = SpeedUnit.Mbps,
                                    MikroTikLocalAddress = mtProfile.LocalAddress,
                                    MikroTikRemoteAddress = mtProfile.RemoteAddress,
                                    MikroTikRateLimit = mtProfile.RateLimit,
                                    MikroTikOnlyOne = mtProfile.OnlyOne,
                                    MikroTikService = mtProfile.Service,
                                    MikroTikServerId = model.MikroTikServerId,
                                    MikroTikProfileId = mtProfile.Id,
                                    NetworkId = networkId.Value, // ربط البروفايل بالشبكة
                                    IsSyncedWithMikroTik = true,
                                    IsActive = !model.ImportAsInactive,
                                    CreatedDate = DateTime.Now,
                                    UpdatedDate = DateTime.Now,
                                    LastSyncDate = DateTime.Now
                                };

                                _context.Profiles.Add(newProfile);
                                importedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failedProfiles.Add($"{profileId}: {MikroTikProfileErrorFormatter.Sanitize(ex.Message, "فشل استيراد البروفايل")}");
                        _logger.LogError(ex, "❌ خطأ في استيراد البروفايل {ProfileId}", profileId);
                    }
                }

                await _context.SaveChangesAsync();

                if (importedCount > 0)
                {
                    decimal chargedAmount = await _profileCompanyWallet.ChargeCompanyForProfileUnitsAsync(
                        companyNetworkId,
                        user!.Id,
                        importedCount,
                        $"خصم استيراد بروفايلات يدوية من السيرفر #{model.MikroTikServerId}");
                    TempData["SuccessMessage"] = chargedAmount > 0m
                        ? $"تم استيراد {importedCount} بروفايل بنجاح، وتم خصم {chargedAmount:N2} ل.س.ج حسب تسعير السرعة/البروفايل المعتمد من مدير النظام."
                        : $"تم استيراد {importedCount} بروفايل بنجاح.";
                }

                if (failedProfiles.Count > 0)
                {
                    TempData["WarningMessage"] = $"فشل استيراد {failedProfiles.Count} بروفايل";
                }

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في استيراد البروفايلات");
                TempData["ErrorMessage"] = MikroTikProfileErrorFormatter.Format("فشل استيراد البروفايلات", ex);
                return RedirectToAction(nameof(ViewMikroTikProfiles), new { serverId = model.MikroTikServerId });
            }
        }

        // GET: Profile/TestSync
        public async Task<IActionResult> TestSync(int serverId, int profileId)
        {
            try
            {
                ApplicationUser? user = await _userManager.GetUserAsync(User);
                int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                Profile? profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == profileId && p.NetworkId == networkId.Value);

                if (profile == null)
                {
                    return Json(new { success = false, message = "البروفايل غير موجود" });
                }

                if (profile.MikroTikServerId <= 0)
                {
                    return Json(new { success = false, message = "لم يتم تحديد خادم MikroTik" });
                }

                // التحقق من أن الخادم يتبع الشبكة المحددة
                MikroTikServer? server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    return Json(new { success = false, message = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه" });
                }

                // التحقق من وجود البروفايل في MikroTik
                bool exists = await _mikroTikService.CheckProfileExistsInMikroTik(
                    profile.MikroTikServerId, profile.Name);

                return Json(new
                {
                    success = true,
                    exists = exists,
                    message = exists ? "البروفايل موجود في MikroTik" : "البروفايل غير موجود في MikroTik"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = MikroTikProfileErrorFormatter.Format("فشل فحص المزامنة", ex) });
            }
        }

        // ===== دوال مساعدة =====

        private void ApplyProfileSyncFailureWarning(SyncResult result)
        {
            if (result.FailedCount <= 0)
            {
                return;
            }

            string detail = result.FailedProfiles != null && result.FailedProfiles.Count > 0
                ? string.Join(" | ", result.FailedProfiles.Take(8))
                : "";
            TempData["WarningMessage"] = string.IsNullOrWhiteSpace(detail)
                ? $"فشلت مزامنة {result.FailedCount} بروفايل — راجع السجلات أو زر «فحص المزامنة» بجانب البروفايل."
                : $"تفاصيل الفشل ({result.FailedCount}): {detail}";
        }

        private bool ProfileExists(int id)
        {
            return _context.Profiles.Any(e => e.Id == id);
        }

        private async Task LoadCreateViewData()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            ApplyCreateFormViewData(await _profileFormViewData.BuildCreateFormDataAsync(networkId));
        }

        private async Task LoadEditViewData()
        {
            ApplicationUser? user = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
            ApplyEditFormViewData(await _profileFormViewData.BuildEditFormDataAsync(networkId));
        }

        private static string GetEnumDescription(Enum value)
        {
            FieldInfo? field = value.GetType().GetField(value.ToString());
            DescriptionAttribute? attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
                .FirstOrDefault() as DescriptionAttribute;

            return attribute?.Description ?? value.ToString();
        }

        private static ProfileType GetProfileTypeFromService(string? service)
        {
            return service?.ToLower() switch
            {
                "pptp" => ProfileType.IPTV,
                "l2tp" => ProfileType.VoIP,
                _ => ProfileType.Internet
            };
        }

    }
}
