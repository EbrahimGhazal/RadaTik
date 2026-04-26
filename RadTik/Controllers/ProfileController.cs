using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Services;
using RadTik.Helpers;
using RadTik.Security;
using RadTik.ViewModels.Profile;
using RadTik.Dtos.MikroTik;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.ComponentModel;

namespace RadTik.Controllers
{
    [Authorize(Roles = "NetworkAdministrator")]
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IMikroTikProfilesService _mikroTikService;
        private readonly ILogger<ProfileController> _logger;
        private readonly UserManager<ApplicationUser> _userManager;

        public ProfileController(
            ApplicationDbContext context,
            IMikroTikProfilesService mikroTikService,
            ILogger<ProfileController> logger,
            UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _mikroTikService = mikroTikService;
            _logger = logger;
            _userManager = userManager;
        }

        // GET: Profile/Index
        public async Task<IActionResult> Index(int? serverId = null)
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

                // جلب خوادم MikroTik للشبكة المحددة
                var servers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .OrderBy(s => s.Name)
                    .ToListAsync();
                
                ViewBag.MikroTikServers = servers;
                ViewBag.SelectedServerId = serverId;

                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                ViewBag.ProfileImportUnitPrice = await GetConfiguredProfileImportUnitPriceAsync();

                // جلب البروفايلات للشبكة المحددة
                IQueryable<Profile> profilesQuery = _context.Profiles
                    .Where(p => p.NetworkId == networkId.Value)
                    .Include(p => p.MikroTikServer)
                    .Include(p => p.Clients);

                if (serverId.HasValue)
                {
                    profilesQuery = profilesQuery.Where(p => p.MikroTikServerId == serverId.Value);
                }

                var profiles = await profilesQuery
                    .OrderBy(p => p.MikroTikServerId)
                    .ThenBy(p => p.DisplayOrder)
                    .ThenBy(p => p.Name)
                    .ToListAsync();

                // إحصاءات
                ViewBag.TotalProfiles = profiles.Count;
                ViewBag.ActiveProfiles = profiles.Count(p => p.IsActive);
                ViewBag.SyncedProfiles = profiles.Count(p => p.IsSyncedWithMikroTik);

                return View(profiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب قائمة البروفايلات");
                TempData["ErrorMessage"] = BuildFriendlyProfileError("خطأ في جلب البيانات", ex);
                return View(new List<Profile>());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetImportPreviewData(int serverId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً." });
                }

                var server = await _context.MikroTikServers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);
                if (server == null)
                {
                    return Json(new { success = false, message = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه." });
                }

                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                var preview = await GetProfilesImportPreviewWithTimeoutAsync(serverId, networkId.Value);
                var configuredUnitPrice = await GetConfiguredProfileImportUnitPriceAsync();
                var configuredTotalCharge = WalletMath.CeilSyp(configuredUnitPrice * preview.ImportableProfilesCount);
                var walletBalance = await GetCompanyWalletBalanceAsync(companyNetworkId);

                return Json(new
                {
                    success = true,
                    serverId,
                    totalProfiles = preview.TotalProfilesOnServer,
                    importableProfiles = preview.ImportableProfilesCount,
                    unitPrice = configuredUnitPrice,
                    totalCharge = configuredTotalCharge,
                    walletBalance
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذر جلب معاينة استيراد البروفايلات");
                return Json(new
                {
                    success = false,
                    message = BuildFriendlyProfileError("تعذر جلب معاينة الاستيراد", ex)
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                var profile = await _context.Profiles
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
                        var mtInfo = await _mikroTikService.GetProfileFromMikroTik(
                            profile.MikroTikServerId,
                            profile.MikroTikProfileId ?? profile.Name);

                        ViewBag.MikroTikInfo = mtInfo;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "⚠️ تعذر جلب معلومات البروفايل من MikroTik: {ErrorMessage}", ex.Message);
                        ViewBag.MikroTikError = BuildFriendlyProfileError("تعذر جلب معلومات البروفايل من MikroTik", ex);
                    }
                }

                return View(profile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في عرض تفاصيل البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("خطأ في عرض التفاصيل", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Profile/Create
        public async Task<IActionResult> Create()
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

                // جلب خوادم MikroTik للشبكة المحددة
                ViewBag.MikroTikServers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .ToListAsync();
                await LoadProfilePricingHintAsync(networkId.Value);
                ViewBag.SystemProfileVatPercentage = await ResolveSystemProfileVatPercentageAsync();

                // إضافة قيم افتراضية لـ ViewData
                ViewData["DefaultDownloadSpeed"] = 10;
                ViewData["DefaultUploadSpeed"] = 10;
                ViewData["DefaultPrice"] = 100;

                // إضافة شرح للحقول
                AddFieldDescriptionsToViewData();

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
                TempData["ErrorMessage"] = BuildFriendlyProfileError("خطأ في تحميل الصفحة", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // POST: Profile/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Profile profile)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    // إعادة تحميل البيانات إذا كان النموذج غير صالح
                    await LoadCreateViewData();
                    return View(profile);
                }

                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    await LoadCreateViewData();
                    return View(profile);
                }

                // ربط البروفايل بالشبكة
                profile.NetworkId = networkId.Value;

                // التحقق من عدم وجود بروفايل بنفس الاسم في نفس الخادم والشبكة
                var existingProfile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Name == profile.Name && p.MikroTikServerId == profile.MikroTikServerId && p.NetworkId == networkId.Value);

                if (existingProfile != null)
                {
                    ModelState.AddModelError("Name", "يوجد بروفايل آخر بنفس الاسم في هذا الخادم");
                    await LoadCreateViewData();
                    return View(profile);
                }

                // إعداد التواريخ
                profile.CreatedDate = DateTime.Now;
                profile.UpdatedDate = DateTime.Now;
                profile.VATPercentage = await ResolveSystemProfileVatPercentageAsync();

                var selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;
                var createCharge = await CalculateProfileChargeAsync(companyNetworkId, 1);
                if (!createCharge.HasSufficientBalance)
                {
                    ModelState.AddModelError(string.Empty,
                        $"لا يمكن إضافة البروفايل حالياً: الرصيد الحالي ({createCharge.WalletBalance:N2}) أقل من المبلغ المطلوب ({createCharge.TotalCharge:N2}) ل.س.ج.");
                    await LoadCreateViewData();
                    return View(profile);
                }

                // إضافة البروفايل إلى MikroTik (MikroTikServerId أصبح required)
                    try
                    {
                        _logger.LogInformation("🚀 محاولة إضافة البروفايل {ProfileName} إلى MikroTik...", profile.Name);

                        // إضافة البروفايل إلى MikroTik
                        var mikrotikId = await _mikroTikService.AddProfileToMikroTik(
                            profile.MikroTikServerId, profile);

                        profile.MikroTikProfileId = mikrotikId;
                        profile.IsSyncedWithMikroTik = true;
                        profile.LastSyncDate = DateTime.Now;

                        _logger.LogInformation("✅ تم إضافة البروفايل {ProfileName} إلى MikroTik بنجاح", profile.Name);
                        TempData["InfoMessage"] = "تمت إضافة البروفايل إلى MikroTik بنجاح";
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "❌ فشل إضافة البروفايل إلى MikroTik: {ErrorMessage}", ex.Message);

                    ModelState.AddModelError("", BuildFriendlyProfileError("فشل إضافة البروفايل إلى MikroTik", ex));
                    await LoadCreateViewData();
                    return View(profile);
                }

                // إضافة البروفايل إلى قاعدة البيانات
                _context.Add(profile);
                await _context.SaveChangesAsync();

                var chargedAmount = await ChargeCompanyForProfileUnitsAsync(
                    companyNetworkId,
                    user!.Id,
                    1,
                    $"خصم إضافة بروفايل جديد: {profile.Name}");

                TempData["SuccessMessage"] = chargedAmount > 0m
                    ? $"تم إنشاء البروفايل '{profile.Name}' بنجاح، وتم خصم {chargedAmount:N2} ل.س.ج حسب تسعير السرعة/البروفايل المعتمد من مدير النظام."
                    : $"تم إنشاء البروفايل '{profile.Name}' بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في إنشاء البروفايل: {ErrorMessage}", ex.Message);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشل إنشاء البروفايل", ex);

                await LoadCreateViewData();
                return View(profile);
            }
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                var profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == id && p.NetworkId == networkId.Value);
                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // جلب خوادم MikroTik للشبكة المحددة
                ViewBag.MikroTikServers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .ToListAsync();
                ViewBag.SystemProfileVatPercentage = await ResolveSystemProfileVatPercentageAsync();

                // إضافة شرح للحقول
                AddFieldDescriptionsToViewData();

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
                TempData["ErrorMessage"] = BuildFriendlyProfileError("خطأ في تحميل الصفحة", ex);
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

                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    await LoadEditViewData();
                    return View(profile);
                }

                var existingProfile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == id && p.NetworkId == networkId.Value);

                if (existingProfile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // التحقق من تغيير السعر
                if (existingProfile.Price != profile.Price)
                {
                    var systemVatPercentage = await ResolveSystemProfileVatPercentageAsync();
                    var priceHistory = new ProfilePriceHistory
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
                        TempData["WarningMessage"] = BuildFriendlyProfileError("تم تحديث البروفايل في قاعدة البيانات ولكن فشل تحديثه في MikroTik", ex);
                    }
                }

                profile.UpdatedDate = DateTime.Now;
                profile.NetworkId = networkId.Value;
                profile.VATPercentage = await ResolveSystemProfileVatPercentageAsync();

                var previousLastSync = existingProfile.LastSyncDate;
                _context.Entry(existingProfile).CurrentValues.SetValues(profile);
                existingProfile.NetworkId = networkId.Value;
                if (!profile.LastSyncDate.HasValue)
                {
                    existingProfile.LastSyncDate = previousLastSync;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"تم تحديث البروفايل '{existingProfile.Name}' بنجاح";
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
                    TempData["ErrorMessage"] = BuildFriendlyProfileError("خطأ في التحديث", ex);
                    await LoadEditViewData();
                    return View(profile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في تحديث البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشل تحديث البروفايل", ex);
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                var profile = await _context.Profiles
                    .Where(p => p.NetworkId == networkId.Value)
                    .Include(p => p.MikroTikServer)
                    .FirstOrDefaultAsync(m => m.Id == id);

                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // التحقق من وجود عملاء مرتبطين في نفس الشبكة
                var clientsCount = await _context.Clients
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
                TempData["ErrorMessage"] = BuildFriendlyProfileError("خطأ في تحميل الصفحة", ex);
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
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    TempData["Error"] = "يرجى تحديد شبكة أولاً";
                    return RedirectToAction("Index", "Network");
                }

                var profile = await _context.Profiles
                    .FirstOrDefaultAsync(p => p.Id == id && p.NetworkId == networkId.Value);
                if (profile == null)
                {
                    TempData["ErrorMessage"] = "البروفايل المطلوب غير موجود";
                    return RedirectToAction(nameof(Index));
                }

                // التحقق من وجود عملاء مرتبطين في نفس الشبكة
                var clientsCount = await _context.Clients
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
                        TempData["WarningMessage"] = BuildFriendlyProfileError("تم حذف البروفايل من قاعدة البيانات ولكن حدث خطأ في حذفه من MikroTik", ex);
                    }
                }

                // حذف من قاعدة البيانات
                _context.Profiles.Remove(profile);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"تم حذف البروفايل '{profile.Name}' بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في حذف البروفايل {ProfileId}", id);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشل حذف البروفايل", ex);
                return RedirectToAction(nameof(Delete), new { id });
            }
        }

        // ===== دوال المزامنة =====

        // GET: Profile/ViewMikroTikProfiles
        public async Task<IActionResult> ViewMikroTikProfiles(int serverId)
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

                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                var profiles = await _mikroTikService.GetProfilesFromMikroTik(serverId);
                ViewBag.MikroTikServer = server;
                ViewBag.AllServers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .ToListAsync();

                return View(profiles);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في جلب البروفايلات من MikroTik للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشل جلب البروفايلات من MikroTik", ex);
                return RedirectToAction(nameof(Index));
            }
        }

        // GET: Profile/SyncFromMikroTik
        public async Task<IActionResult> SyncFromMikroTik(int serverId, bool importAsInactive = false, decimal defaultPrice = 100m)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                var preview = await _mikroTikService.BuildProfilesImportPreviewAsync(serverId, networkId.Value);
                if (preview.ImportableProfilesCount <= 0)
                {
                    TempData["InfoMessage"] = "لا يوجد بروفايلات جديدة للاستيراد من هذا السيرفر. إذا أردت تحديث البروفايلات الحالية استخدم «المزامنة الثنائية».";
                    return RedirectToAction(nameof(Index));
                }

                var syncCharge = await CalculateProfileChargeAsync(companyNetworkId, preview.ImportableProfilesCount);
                if (!syncCharge.HasSufficientBalance)
                {
                    TempData["ErrorMessage"] =
                        $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({syncCharge.WalletBalance:N2}) أقل من المبلغ المطلوب ({syncCharge.TotalCharge:N2}) ل.س.ج.";
                    return RedirectToAction(nameof(Index));
                }

                if (defaultPrice < 0m)
                {
                    defaultPrice = 0m;
                }

                if (defaultPrice > 1_000_000m)
                {
                    defaultPrice = 1_000_000m;
                }

                var result = await _mikroTikService.SyncFromMikroTikToDatabase(serverId, importAsInactive, networkId.Value, defaultPrice);

                if (result.Success)
                {
                    var chargedAmount = 0m;
                    if (result.AddedCount > 0)
                    {
                        chargedAmount = await ChargeCompanyForProfileUnitsAsync(
                            companyNetworkId,
                            user!.Id,
                            result.AddedCount,
                            $"خصم استيراد بروفايلات من السيرفر #{serverId}");
                    }
                    if (result.AddedCount > 0 || result.UpdatedCount > 0)
                    {
                        TempData["SuccessMessage"] = chargedAmount > 0m
                            ? $"{result.Message} وتم خصم {chargedAmount:N2} ل.س.ج مقابل {result.AddedCount} بروفايل مستورد."
                            : result.Message;
                    }
                    else
                    {
                        TempData["InfoMessage"] = "جميع البروفايلات محدثة بالفعل";
                    }

                    ApplyProfileSyncFailureWarning(result);
                }
                else
                {
                    TempData["ErrorMessage"] = SanitizeMikroTikMessage(result.Message, "فشلت المزامنة");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في مزامنة البروفايلات من MikroTik للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشلت المزامنة", ex);
            }

            return RedirectToAction(nameof(Index), new { serverId });
        }

        // GET: Profile/SyncToMikroTik
        public async Task<IActionResult> SyncToMikroTik(int serverId)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    TempData["ErrorMessage"] = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه";
                    return RedirectToAction(nameof(Index));
                }

                var result = await _mikroTikService.SyncFromDatabaseToMikroTik(serverId, networkId.Value);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    ApplyProfileSyncFailureWarning(result);
                }
                else
                {
                    TempData["ErrorMessage"] = SanitizeMikroTikMessage(result.Message, "فشلت المزامنة");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في مزامنة البروفايلات إلى MikroTik للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشلت المزامنة", ex);
            }

            return RedirectToAction(nameof(Index), new { serverId });
        }

        // GET: Profile/TwoWaySync
        public async Task<IActionResult> TwoWaySync(int serverId, decimal defaultImportPrice = 100m)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
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

                var result = await _mikroTikService.TwoWaySync(serverId, networkId.Value, defaultImportPrice);

                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    ApplyProfileSyncFailureWarning(result);
                }
                else
                {
                    TempData["ErrorMessage"] = SanitizeMikroTikMessage(result.Message, "فشلت المزامنة");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ خطأ في المزامنة الثنائية للخادم {ServerId}", serverId);
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشلت المزامنة", ex);
            }

            return RedirectToAction(nameof(Index), new { serverId });
        }

        // POST: Profile/ImportFromMikroTik
        [HttpPost]
        public async Task<IActionResult> ImportFromMikroTik(ImportProfileViewModel model)
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

                // التحقق من أن الخادم يتبع الشبكة المحددة
                var server = await _context.MikroTikServers
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

                var selectedNetwork = await _context.Networks
                    .AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == networkId.Value);
                var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId.Value;

                var importPreview = await _mikroTikService.BuildProfilesImportPreviewAsync(model.MikroTikServerId, networkId.Value);
                if (importPreview.ImportableProfilesCount <= 0)
                {
                    TempData["InfoMessage"] = "لا يوجد بروفايلات جديدة للاستيراد من هذا السيرفر. إذا أردت تحديث البروفايلات الحالية استخدم «المزامنة الثنائية».";
                    return RedirectToAction(nameof(Index));
                }

                var mikrotikProfiles = await _mikroTikService.GetProfilesFromMikroTik(model.MikroTikServerId);
                var existingNames = (await _context.Profiles
                    .AsNoTracking()
                    .Where(p => p.MikroTikServerId == model.MikroTikServerId && p.NetworkId == networkId.Value && !string.IsNullOrEmpty(p.Name))
                    .Select(p => p.Name)
                    .ToListAsync())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var selectedImportableCount = mikrotikProfiles
                    .Where(p => model.SelectedProfileIds.Contains(p.Id) && !existingNames.Contains(p.Name))
                    .Count();

                var importCharge = await CalculateProfileChargeAsync(companyNetworkId, selectedImportableCount);
                if (!importCharge.HasSufficientBalance)
                {
                    TempData["ErrorMessage"] =
                        $"❌ لا يمكن تنفيذ الاستيراد: الرصيد الحالي ({importCharge.WalletBalance:N2}) أقل من المبلغ المطلوب ({importCharge.TotalCharge:N2}) ل.س.ج.";
                    return RedirectToAction(nameof(Index));
                }

                var importedCount = 0;
                var failedProfiles = new List<string>();

                foreach (var profileId in model.SelectedProfileIds)
                {
                    try
                    {
                        var mtProfile = mikrotikProfiles.FirstOrDefault(p => p.Id == profileId);
                        if (mtProfile != null)
                        {
                            // التحقق من عدم وجود البروفايل مسبقاً في نفس الشبكة
                            var existingProfile = await _context.Profiles
                                .FirstOrDefaultAsync(p => p.Name == mtProfile.Name && p.MikroTikServerId == model.MikroTikServerId && p.NetworkId == networkId.Value);

                            if (existingProfile == null)
                            {
                                var newProfile = new Profile
                                {
                                    Name = mtProfile.Name,
                                    Description = $"مستورد من MikroTik - {DateTime.Now:yyyy-MM-dd}",
                                    Type = GetProfileTypeFromService(mtProfile.Service),
                                    BillingCycle = BillingCycle.Monthly,
                                    Price = model.SetDefaultPrice ? model.DefaultPrice : 0,
                                    VATPercentage = await ResolveSystemProfileVatPercentageAsync(),
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
                        failedProfiles.Add($"{profileId}: {SanitizeMikroTikMessage(ex.Message, "فشل استيراد البروفايل")}");
                        _logger.LogError(ex, "❌ خطأ في استيراد البروفايل {ProfileId}", profileId);
                    }
                }

                await _context.SaveChangesAsync();

                if (importedCount > 0)
                {
                    var chargedAmount = await ChargeCompanyForProfileUnitsAsync(
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
                TempData["ErrorMessage"] = BuildFriendlyProfileError("فشل استيراد البروفايلات", ex);
                return RedirectToAction(nameof(ViewMikroTikProfiles), new { serverId = model.MikroTikServerId });
            }
        }

        // GET: Profile/TestSync
        public async Task<IActionResult> TestSync(int serverId, int profileId)
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

                if (!networkId.HasValue)
                {
                    return Json(new { success = false, message = "يرجى تحديد شبكة أولاً" });
                }

                var profile = await _context.Profiles
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
                var server = await _context.MikroTikServers
                    .FirstOrDefaultAsync(s => s.Id == serverId && s.NetworkId == networkId.Value);

                if (server == null)
                {
                    return Json(new { success = false, message = "الخادم غير موجود أو ليس لديك صلاحية للوصول إليه" });
                }

                // التحقق من وجود البروفايل في MikroTik
                var exists = await _mikroTikService.CheckProfileExistsInMikroTik(
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
                return Json(new { success = false, message = BuildFriendlyProfileError("فشل فحص المزامنة", ex) });
            }
        }

        // ===== دوال مساعدة =====

        private void ApplyProfileSyncFailureWarning(SyncResult result)
        {
            if (result.FailedCount <= 0)
            {
                return;
            }

            var detail = result.FailedProfiles != null && result.FailedProfiles.Count > 0
                ? string.Join(" | ", result.FailedProfiles.Take(8))
                : "";
            TempData["WarningMessage"] = string.IsNullOrWhiteSpace(detail)
                ? $"فشلت مزامنة {result.FailedCount} بروفايل — راجع السجلات أو زر «فحص المزامنة» بجانب البروفايل."
                : $"تفاصيل الفشل ({result.FailedCount}): {detail}";
        }

        private async Task<ImportProfilesPreviewResult> GetProfilesImportPreviewWithTimeoutAsync(int serverId, int networkId)
        {
            const int previewTimeoutMs = 5000;
            var previewTask = _mikroTikService.BuildProfilesImportPreviewAsync(serverId, networkId);
            var completed = await Task.WhenAny(previewTask, Task.Delay(previewTimeoutMs));
            if (completed == previewTask)
            {
                return await previewTask;
            }

            throw new TimeoutException($"Profile import preview timed out after {previewTimeoutMs}ms.");
        }

        private static string BuildFriendlyProfileError(string prefix, Exception ex)
        {
            return SanitizeMikroTikMessage(ex.Message, prefix);
        }

        private static string SanitizeMikroTikMessage(string? rawMessage, string prefix)
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

        private bool ProfileExists(int id)
        {
            return _context.Profiles.Any(e => e.Id == id);
        }

        private async Task LoadCreateViewData()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (networkId.HasValue)
            {
                ViewBag.MikroTikServers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .ToListAsync();
                await LoadProfilePricingHintAsync(networkId.Value);
                ViewBag.SystemProfileVatPercentage = await ResolveSystemProfileVatPercentageAsync();
            }
            else
            {
                ViewBag.MikroTikServers = await _context.MikroTikServers
                    .Where(s => s.IsActive)
                    .ToListAsync();
                ViewBag.ProfileCreateUnitPrice = 0m;
                ViewBag.SystemProfileVatPercentage = 15m;
            }
            AddFieldDescriptionsToViewData();
        }

        private async Task LoadProfilePricingHintAsync(int networkId)
        {
            var selectedNetwork = await _context.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == networkId);
            var companyNetworkId = selectedNetwork?.ParentNetworkId ?? networkId;
            var estimate = await CalculateProfileChargeAsync(companyNetworkId, 1);
            ViewBag.ProfileCreateUnitPrice = estimate.UnitPrice;
        }

        private async Task LoadEditViewData()
        {
            var user = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);

            if (networkId.HasValue)
            {
                ViewBag.MikroTikServers = await _context.MikroTikServers
                    .Where(s => s.IsActive && s.NetworkId == networkId.Value)
                    .ToListAsync();
                ViewBag.SystemProfileVatPercentage = await ResolveSystemProfileVatPercentageAsync();
            }
            else
            {
                ViewBag.MikroTikServers = await _context.MikroTikServers
                    .Where(s => s.IsActive)
                    .ToListAsync();
                ViewBag.SystemProfileVatPercentage = 15m;
            }
            AddFieldDescriptionsToViewData();
        }

        private async Task<decimal> ResolveSystemProfileVatPercentageAsync()
        {
            var taxRow = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.ProfilePriceTax &&
                    p.ChargeUnit == PricingChargeUnit.Flat &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            if (taxRow == null)
            {
                return 15m;
            }

            var tax = taxRow.AmountSYP;

            if (tax < 0m)
            {
                return 0m;
            }

            if (tax > 100m)
            {
                return 100m;
            }

            return tax;
        }

        private void AddFieldDescriptionsToViewData()
        {
            // إضافة شرح لكل حقل من خلال Reflection
            var properties = typeof(Profile).GetProperties();
            foreach (var prop in properties)
            {
                var descriptionAttr = prop.GetCustomAttributes(typeof(DescriptionAttribute), true)
                    .FirstOrDefault() as DescriptionAttribute;

                if (descriptionAttr != null)
                {
                    ViewData[$"{prop.Name}_Description"] = descriptionAttr.Description;
                }
            }
        }

        private static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttributes(typeof(DescriptionAttribute), false)
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

        private async Task<decimal> GetConfiguredProfileImportUnitPriceAsync()
        {
            return await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.Profiles &&
                    p.ChargeUnit == PricingChargeUnit.PerSpeedProfile &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime)
                .OrderByDescending(p => p.UpdatedAt)
                .ThenByDescending(p => p.Id)
                .Select(p => p.AmountSYP)
                .FirstOrDefaultAsync();
        }

        private async Task<decimal> GetCompanyWalletBalanceAsync(int companyNetworkId)
        {
            return await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == companyNetworkId && n.ParentNetworkId == null)
                .Select(n => n.Balance)
                .FirstOrDefaultAsync();
        }

        private async Task<(decimal UnitPrice, decimal TotalCharge, decimal WalletBalance, bool HasSufficientBalance)> CalculateProfileChargeAsync(
            int companyNetworkId,
            int unitsCount)
        {
            var walletBalance = await GetCompanyWalletBalanceAsync(companyNetworkId);
            if (unitsCount <= 0)
            {
                return (0m, 0m, walletBalance, true);
            }

            var unitPrice = WalletMath.CeilSyp(await GetConfiguredProfileImportUnitPriceAsync());
            var totalCharge = WalletMath.CeilSyp(unitPrice * unitsCount);
            return (unitPrice, totalCharge, walletBalance, walletBalance >= totalCharge);
        }

        private async Task<decimal> ChargeCompanyForProfileUnitsAsync(
            int companyNetworkId,
            string actorUserId,
            int unitsCount,
            string note)
        {
            if (unitsCount <= 0)
            {
                return 0m;
            }

            var charge = await CalculateProfileChargeAsync(companyNetworkId, unitsCount);
            if (charge.TotalCharge <= 0m)
            {
                return 0m;
            }

            var company = await _context.Networks
                .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null);
            if (company == null)
            {
                return 0m;
            }

            if (company.Balance < charge.TotalCharge)
            {
                throw new InvalidOperationException(
                    $"Insufficient company balance. Required={charge.TotalCharge}, Balance={company.Balance}");
            }

            var previousBalance = company.Balance;
            company.Balance -= charge.TotalCharge;

            _context.NetworkWalletTransactions.Add(new NetworkWalletTransaction
            {
                NetworkId = companyNetworkId,
                Type = NetworkWalletTransactionType.ServiceCharge,
                SignedAmount = -charge.TotalCharge,
                PreviousBalance = previousBalance,
                NewBalance = company.Balance,
                CreatedByUserId = actorUserId,
                CreatedAt = DateTime.Now,
                Notes = $"{note} | العدد: {unitsCount} | سعر الوحدة: {charge.UnitPrice:N2} ل.س.ج | الإجمالي: {charge.TotalCharge:N2} ل.س.ج"
            });

            await _context.SaveChangesAsync();
            return charge.TotalCharge;
        }
    }
}