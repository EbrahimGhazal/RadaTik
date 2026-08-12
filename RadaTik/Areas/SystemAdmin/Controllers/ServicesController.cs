using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPolicies;
using System.Text.Json;

namespace RadaTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<ServicesController> _logger;
        private readonly UsageBasedSubscriptionChargeService _usageChargeService;
        private readonly IFeaturePublicContentComposer _contentComposer;

        public ServicesController(
            ApplicationDbContext context,
            ILogger<ServicesController> logger,
            UsageBasedSubscriptionChargeService usageChargeService,
            IFeaturePublicContentComposer contentComposer)
        {
            _context = context;
            _logger = logger;
            _usageChargeService = usageChargeService;
            _contentComposer = contentComposer;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "خدمات الشركات (التسعير)";

            var items = await _context.FeaturePricings
                .AsNoTracking()
                .OrderBy(p => p.FeatureKey)
                .ThenBy(p => p.BillingPeriod)
                .ToListAsync();

            ViewBag.CustomServices = await _context.SystemServices
                .AsNoTracking()
                .OrderBy(s => s.DisplayName)
                .ToListAsync();

            ViewBag.FeaturePublicInfos = await _context.FeaturePublicInfos
                .AsNoTracking()
                .OrderBy(f => f.FeatureKey)
                .ToListAsync();

            var featureNameByKey = FeatureCatalog.All
                .ToDictionary(f => f.Key, f => f.DisplayName, StringComparer.OrdinalIgnoreCase);
            featureNameByKey[FeatureKeys.ReportsExport] = "سعر توليد التقرير";
            featureNameByKey[FeatureKeys.CollectionCommission] = "عمولة التحصيل";
            featureNameByKey[FeatureKeys.MaintenanceTransportFee] = "أجور نقل الصيانة";
            featureNameByKey[FeatureKeys.MaintenanceCommission] = "عمولة الصيانة";

            var customServices = (ViewBag.CustomServices as IEnumerable<SystemService>)?.ToList() ?? [];
            foreach (var service in customServices)
            {
                if (!featureNameByKey.ContainsKey(service.Key))
                {
                    featureNameByKey[service.Key] = service.DisplayName;
                }
            }

            var allFeatureKeys = new HashSet<string>(featureNameByKey.Keys, StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                allFeatureKeys.Add(item.FeatureKey);
            }

            var pricingByFeature = items
                .GroupBy(p => p.FeatureKey, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<FeaturePricing>)g.ToList(), StringComparer.OrdinalIgnoreCase);

            var generatedContent = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var simulation = new Dictionary<string, PricingSimulationDescriptor>(StringComparer.OrdinalIgnoreCase);

            foreach (var key in allFeatureKeys.OrderBy(k => k))
            {
                var displayName = featureNameByKey.TryGetValue(key, out var nm) ? nm : key;
                var keyPricings = pricingByFeature.TryGetValue(key, out var pp) ? pp : [];
                var draft = _contentComposer.Compose(key, displayName, keyPricings);
                generatedContent[key] = new
                {
                    detailHtml = draft.DetailHtml,
                    pricingPolicyHtml = draft.PricingPolicyHtml
                };
                simulation[key] = _contentComposer.BuildSimulationDescriptor(key, displayName, keyPricings);
            }

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            ViewBag.GeneratedPublicInfosJson = JsonSerializer.Serialize(generatedContent, jsonOptions);
            ViewBag.PricingSimulationJson = JsonSerializer.Serialize(simulation, jsonOptions);

            ViewBag.Items = items;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveFeaturePublicInfo(string featureKey, string? detailHtml, string? pricingPolicyHtml)
        {
            featureKey = (featureKey ?? "").Trim();
            if (string.IsNullOrEmpty(featureKey))
            {
                TempData["Error"] = "يرجى اختيار مفتاح الخدمة.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var row = await _context.FeaturePublicInfos.FirstOrDefaultAsync(f => f.FeatureKey == featureKey);
                if (row == null)
                {
                    row = new FeaturePublicInfo { FeatureKey = featureKey };
                    _context.FeaturePublicInfos.Add(row);
                }

                row.DetailHtml = string.IsNullOrWhiteSpace(detailHtml) ? null : detailHtml.Trim();
                row.PricingPolicyHtml = string.IsNullOrWhiteSpace(pricingPolicyHtml) ? null : pricingPolicyHtml.Trim();
                row.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                TempData["Success"] = "تم حفظ نصوص «عرض التفاصيل» لمديري الشركات.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "SaveFeaturePublicInfo failed for {Key}", featureKey);
                TempData["Error"] = "تعذر حفظ المحتوى.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreatePricing(string featureKey, PricingBillingPeriod billingPeriod, PricingChargeUnit chargeUnit, decimal amountSYP, decimal amountUSD, bool isActive = true)
        {
            if (string.IsNullOrWhiteSpace(featureKey))
            {
                if (IsAjaxRequest()) return Json(new { ok = false, message = "مفتاح الخدمة غير صالح." });
                TempData["Error"] = "مفتاح الخدمة غير صالح.";
                return RedirectToAction(nameof(Index));
            }

            featureKey = featureKey.Trim();

            (amountSYP, amountUSD) = NormalizeAmountsForChargeUnit(chargeUnit, amountSYP, amountUSD);

            if (string.Equals(featureKey, FeatureKeys.CollectionCommission, StringComparison.OrdinalIgnoreCase))
            {
                if (chargeUnit != PricingChargeUnit.PercentOfCollectedAmount || billingPeriod != PricingBillingPeriod.OneTime)
                {
                    if (IsAjaxRequest()) return Json(new { ok = false, message = "عمولة التحصيل: اختر «مرة واحدة» و«% من مبلغ التحصيل»." });
                    TempData["Error"] = "عمولة التحصيل: اختر «مرة واحدة» و«% من مبلغ التحصيل».";
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (string.Equals(featureKey, FeatureKeys.MaintenanceTransportFee, StringComparison.OrdinalIgnoreCase))
            {
                if (chargeUnit != PricingChargeUnit.Flat || billingPeriod != PricingBillingPeriod.OneTime)
                {
                    if (IsAjaxRequest()) return Json(new { ok = false, message = "أجور نقل الصيانة: اختر «مرة واحدة» و«ثابت»." });
                    TempData["Error"] = "أجور نقل الصيانة: اختر «مرة واحدة» و«ثابت».";
                    return RedirectToAction(nameof(Index));
                }
            }
            else if (string.Equals(featureKey, FeatureKeys.MaintenanceCommission, StringComparison.OrdinalIgnoreCase))
            {
                var isAllowed = chargeUnit == PricingChargeUnit.Flat || chargeUnit == PricingChargeUnit.PercentOfCollectedAmount;
                if (!isAllowed || billingPeriod != PricingBillingPeriod.OneTime)
                {
                    if (IsAjaxRequest()) return Json(new { ok = false, message = "عمولة الصيانة: اختر «مرة واحدة» و«ثابت» أو «% من مبلغ التحصيل»." });
                    TempData["Error"] = "عمولة الصيانة: اختر «مرة واحدة» و«ثابت» أو «% من مبلغ التحصيل».";
                    return RedirectToAction(nameof(Index));
                }
            }

            try
            {
                var existsSamePeriod = await _context.FeaturePricings.AnyAsync(p =>
                    p.FeatureKey == featureKey &&
                    p.BillingPeriod == billingPeriod);
                if (existsSamePeriod)
                {
                    if (IsAjaxRequest()) return Json(new { ok = false, message = "يوجد تسعير بنفس الخدمة ومدة الاستحقاق مسبقاً." });
                    TempData["Error"] = "يوجد تسعير بنفس الخدمة ومدة الاستحقاق مسبقاً.";
                    return RedirectToAction(nameof(Index));
                }

                var row = new FeaturePricing
                {
                    FeatureKey = featureKey,
                    BillingPeriod = billingPeriod,
                    ChargeUnit = chargeUnit,
                    AmountSYP = amountSYP,
                    AmountUSD = amountUSD,
                    Currency = PricingCurrency.SYP_New,
                    IsActive = isActive,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };

                _context.FeaturePricings.Add(row);

                await _context.SaveChangesAsync();

                await TryAutoProvisionCustomServiceSubscriptionsAsync(featureKey, row);

                if (IsAjaxRequest())
                {
                    return Json(new
                    {
                        ok = true,
                        message = "تمت إضافة تسعير الخدمة بنجاح.",
                        item = new
                        {
                            id = row.Id,
                            featureKey = row.FeatureKey,
                            billingPeriod = (int)row.BillingPeriod,
                            chargeUnit = (int)row.ChargeUnit,
                            amountSYP = row.AmountSYP,
                            amountUSD = row.AmountUSD,
                            isActive = row.IsActive
                        }
                    });
                }
                TempData["Success"] = "تمت إضافة تسعير الخدمة بنجاح.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create FeaturePricing.");
                if (IsAjaxRequest()) return Json(new { ok = false, message = "تعذر إضافة التسعير." });
                TempData["Error"] = "تعذر إضافة التسعير.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePricing(int id, decimal amountSYP, decimal amountUSD, PricingBillingPeriod billingPeriod, PricingChargeUnit chargeUnit, bool isActive = true)
        {
            try
            {
                var row = await _context.FeaturePricings.FindAsync(id);
                if (row == null)
                {
                    if (IsAjaxRequest()) return Json(new { ok = false, message = "التسعير غير موجود." });
                    return NotFound();
                }

                // Allow changing billing period if it does not conflict with unique constraint
                if (row.BillingPeriod != billingPeriod)
                {
                    var exists = await _context.FeaturePricings.AnyAsync(p => p.Id != id && p.FeatureKey == row.FeatureKey && p.BillingPeriod == billingPeriod);
                    if (exists)
                    {
                        if (IsAjaxRequest()) return Json(new { ok = false, message = "لا يمكن تغيير مدة الاستحقاق لأنها موجودة مسبقاً لنفس الخدمة." });
                        TempData["Error"] = "لا يمكن تغيير مدة الاستحقاق لأنها موجودة مسبقاً لنفس الخدمة.";
                        return RedirectToAction(nameof(Index));
                    }
                }

                if (string.Equals(row.FeatureKey, FeatureKeys.CollectionCommission, StringComparison.OrdinalIgnoreCase))
                {
                    if (chargeUnit != PricingChargeUnit.PercentOfCollectedAmount || billingPeriod != PricingBillingPeriod.OneTime)
                    {
                        if (IsAjaxRequest()) return Json(new { ok = false, message = "عمولة التحصيل: اختر «مرة واحدة» و«% من مبلغ التحصيل»." });
                        TempData["Error"] = "عمولة التحصيل: اختر «مرة واحدة» و«% من مبلغ التحصيل».";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else if (string.Equals(row.FeatureKey, FeatureKeys.MaintenanceTransportFee, StringComparison.OrdinalIgnoreCase))
                {
                    if (chargeUnit != PricingChargeUnit.Flat || billingPeriod != PricingBillingPeriod.OneTime)
                    {
                        if (IsAjaxRequest()) return Json(new { ok = false, message = "أجور نقل الصيانة: اختر «مرة واحدة» و«ثابت»." });
                        TempData["Error"] = "أجور نقل الصيانة: اختر «مرة واحدة» و«ثابت».";
                        return RedirectToAction(nameof(Index));
                    }
                }
                else if (string.Equals(row.FeatureKey, FeatureKeys.MaintenanceCommission, StringComparison.OrdinalIgnoreCase))
                {
                    var isAllowed = chargeUnit == PricingChargeUnit.Flat || chargeUnit == PricingChargeUnit.PercentOfCollectedAmount;
                    if (!isAllowed || billingPeriod != PricingBillingPeriod.OneTime)
                    {
                        if (IsAjaxRequest()) return Json(new { ok = false, message = "عمولة الصيانة: اختر «مرة واحدة» و«ثابت» أو «% من مبلغ التحصيل»." });
                        TempData["Error"] = "عمولة الصيانة: اختر «مرة واحدة» و«ثابت» أو «% من مبلغ التحصيل».";
                        return RedirectToAction(nameof(Index));
                    }
                }

                (amountSYP, amountUSD) = NormalizeAmountsForChargeUnit(chargeUnit, amountSYP, amountUSD);

                row.BillingPeriod = billingPeriod;
                row.ChargeUnit = chargeUnit;
                row.AmountSYP = amountSYP;
                row.AmountUSD = amountUSD;
                row.IsActive = isActive;
                row.Currency = PricingCurrency.SYP_New;
                row.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                if (IsAjaxRequest())
                {
                    return Json(new
                    {
                        ok = true,
                        message = "تم تحديث تسعير الخدمة.",
                        item = new
                        {
                            id = row.Id,
                            featureKey = row.FeatureKey,
                            billingPeriod = (int)row.BillingPeriod,
                            chargeUnit = (int)row.ChargeUnit,
                            amountSYP = row.AmountSYP,
                            amountUSD = row.AmountUSD,
                            isActive = row.IsActive
                        }
                    });
                }
                TempData["Success"] = "تم تحديث تسعير الخدمة.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update FeaturePricing.");
                if (IsAjaxRequest()) return Json(new { ok = false, message = "تعذر تحديث التسعير." });
                TempData["Error"] = "تعذر تحديث التسعير.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePricing(int id)
        {
            try
            {
                var row = await _context.FeaturePricings.FindAsync(id);
                if (row == null)
                {
                    if (IsAjaxRequest()) return Json(new { ok = false, message = "التسعير غير موجود." });
                    return NotFound();
                }

                var featureKey = row.FeatureKey;
                _context.FeaturePricings.Remove(row);
                await _context.SaveChangesAsync();
                if (IsAjaxRequest()) return Json(new { ok = true, message = "تم حذف تسعير الخدمة.", featureKey });
                TempData["Success"] = "تم حذف تسعير الخدمة.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete FeaturePricing.");
                if (IsAjaxRequest()) return Json(new { ok = false, message = "تعذر حذف التسعير." });
                TempData["Error"] = "تعذر حذف التسعير.";
            }

            return RedirectToAction(nameof(Index));
        }

        private bool IsAjaxRequest()
        {
            return string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// التسعير الحالي يعتمد على الليرة السورية الجديدة فقط.
        /// في حال النسبة المئوية تُخزَّن النسبة في AmountSYP (5 = 5%).
        /// AmountUSD يُصفّر دائماً في هذه المرحلة.
        /// </summary>
        private static (decimal AmountSyp, decimal AmountUsd) NormalizeAmountsForChargeUnit(
            PricingChargeUnit chargeUnit,
            decimal amountSYP,
            decimal amountUSD)
        {
            if (chargeUnit != PricingChargeUnit.PercentOfCollectedAmount)
            {
                return (amountSYP < 0m ? 0m : amountSYP, 0m);
            }

            var syp = amountSYP < 0m ? 0m : amountSYP;
            if (syp > 100m)
            {
                syp = 100m;
            }

            return (syp, 0m);
        }

        /// <summary>
        /// خدمات مخصصة (CUSTOM:*) بعد تسعيرها من مدير النظام تُفعَّل تلقائياً لجميع شركات الشبكات الرئيسية.
        /// </summary>
        private async Task TryAutoProvisionCustomServiceSubscriptionsAsync(string featureKey, FeaturePricing pricing)
        {
            if (!featureKey.StartsWith("CUSTOM:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!pricing.IsActive)
            {
                return;
            }

            var svcExists = await _context.SystemServices.AsNoTracking()
                .AnyAsync(s => s.Key == featureKey && s.IsActive);
            if (!svcExists)
            {
                return;
            }

            if (string.Equals(featureKey, FeatureKeys.CollectionCommission, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var companyNetworkIds = await _context.Networks.AsNoTracking()
                .Where(n => n.ParentNetworkId == null)
                .Select(n => n.Id)
                .ToListAsync();

            var now = DateTime.Now;

            foreach (var networkId in companyNetworkIds)
            {
                var existing = await _context.NetworkServiceSubscriptions
                    .FirstOrDefaultAsync(s => s.NetworkId == networkId && s.FeatureKey == featureKey);

                if (existing != null &&
                    existing.Status == NetworkServiceSubscriptionStatus.Active &&
                    existing.ExpiresAt > now)
                {
                    continue;
                }

                if (existing == null)
                {
                    var sub = new NetworkServiceSubscription
                    {
                        NetworkId = networkId,
                        FeatureKey = featureKey,
                        BillingPeriod = pricing.BillingPeriod,
                        StartAt = now,
                        ExpiresAt = AddBillingPeriod(now, pricing.BillingPeriod),
                        Status = NetworkServiceSubscriptionStatus.Active,
                        CreatedAt = now,
                        UpdatedAt = now,
                        LastApprovedRequestId = null
                    };
                    _context.NetworkServiceSubscriptions.Add(sub);
                    await _context.SaveChangesAsync();
                    await _usageChargeService.InitializeBaselineAsync(networkId, sub.Id);
                }
                else
                {
                    var baseDate = existing.ExpiresAt > now ? existing.ExpiresAt : now;
                    existing.BillingPeriod = pricing.BillingPeriod;
                    existing.Status = NetworkServiceSubscriptionStatus.Active;
                    existing.StartAt = existing.StartAt == default ? now : existing.StartAt;
                    existing.ExpiresAt = AddBillingPeriod(baseDate, pricing.BillingPeriod);
                    existing.UpdatedAt = now;
                    await _context.SaveChangesAsync();
                    await _usageChargeService.InitializeBaselineAsync(networkId, existing.Id);
                }
            }
        }

        private static DateTime AddBillingPeriod(DateTime baseDate, PricingBillingPeriod billingPeriod)
        {
            return billingPeriod switch
            {
                PricingBillingPeriod.Daily => baseDate.AddDays(1),
                PricingBillingPeriod.Monthly => baseDate.AddMonths(1),
                PricingBillingPeriod.Every3Months => baseDate.AddMonths(3),
                PricingBillingPeriod.Every6Months => baseDate.AddMonths(6),
                PricingBillingPeriod.Every12Months => baseDate.AddYears(1),
                _ => baseDate.AddYears(10)
            };
        }
    }
}

