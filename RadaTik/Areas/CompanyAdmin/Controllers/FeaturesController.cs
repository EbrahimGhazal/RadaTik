using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.ViewModels.CompanyAdmin;
using System.Net;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

/// <summary>
/// مركز خدمات الشركة: عرض الاشتراكات، التسعير، وطلب تفعيل خدمة جديدة.
/// </summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class FeaturesController(
    ApplicationDbContext context,
    UserManager<ApplicationUser> userManager,
    IUsageBasedSubscriptionChargeService usageSubscriptionChargeService,
    ILogger<FeaturesController> logger) : Controller
{
    private static readonly HashSet<string> NonSubscribableFeatureKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        FeatureKeys.CollectionCommission,
        FeatureKeys.ReportsExport,
        FeatureKeys.MaintenanceCommission,
        FeatureKeys.MaintenanceTransportFee,
        FeatureKeys.ProfilePriceTax
    };

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "الخدمات والاشتراكات";
        ViewData["BodyClass"] = "manager-dashboard-page features-hub-page";

        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            TempData["Info"] = "يرجى إنشاء شبكة أولاً قبل إضافة الخدمات.";
            return RedirectToAction("Create", "Network", new { area = "CompanyAdmin" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, context, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(context, user, userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        Network? selectedNetwork = await context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            TempData["Error"] = "تعذر العثور على الشبكة المحددة.";
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effectiveNetwork = selectedNetwork.ParentNetworkId.HasValue
            ? await context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;

        if (effectiveNetwork?.ParentNetworkId == null)
        {
            try
            {
                await CompanySubscriptionBootstrap.EnsureFullCompanyEntitlementsAsync(
                    context,
                    usageSubscriptionChargeService,
                    effectiveNetworkId,
                    logger);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "تعذر مزامنة تفعيل الخدمات للشبكة #{NetworkId} عند فتح مركز الخدمات",
                    effectiveNetworkId);
            }
        }

        CompanyServicesIndexViewModel vm = new()
        {
            SelectedNetworkId = selectedNetwork.Id,
            SelectedNetworkName = selectedNetwork.Name,
            EffectiveCompanyNetworkId = effectiveNetworkId,
            EffectiveCompanyNetworkName = effectiveNetwork?.Name ?? selectedNetwork.Name,
            CompanyBalance = effectiveNetwork?.Balance ?? 0m,
            Services = await BuildServicesListAsync(effectiveNetworkId)
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestSubscription(int pricingId)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, context, user) ?? user.NetworkId;
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        int? effectiveNetworkId = await CompanyServiceEntitlementResolver.ResolveEffectiveCompanyNetworkIdAsync(
            context,
            selectedNetworkId);
        if (!effectiveNetworkId.HasValue)
        {
            TempData["Error"] = "تعذر تحديد شبكة الشركة.";
            return RedirectToAction(nameof(Index));
        }

        FeaturePricing? pricing = await context.FeaturePricings
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pricingId && p.IsActive);

        if (pricing == null || NonSubscribableFeatureKeys.Contains(pricing.FeatureKey))
        {
            TempData["Error"] = "خطة التسعير المحددة غير متاحة.";
            return RedirectToAction(nameof(Index));
        }

        if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
        {
            TempData["Error"] = "لا يمكن طلب هذه الخدمة كاشتراك.";
            return RedirectToAction(nameof(Index));
        }

        DateTime now = DateTime.Now;

        bool alreadyActive = await context.NetworkServiceSubscriptions
            .AsNoTracking()
            .AnyAsync(s =>
                s.NetworkId == effectiveNetworkId.Value &&
                s.FeatureKey == pricing.FeatureKey &&
                s.Status == NetworkServiceSubscriptionStatus.Active &&
                s.ExpiresAt > now);

        if (alreadyActive)
        {
            TempData["Info"] = "الخدمة مفعّلة بالفعل لدى شركتك.";
            return RedirectToAction(nameof(Index), new { highlight = pricing.FeatureKey });
        }

        bool hasPending = await context.NetworkServiceRequests
            .AsNoTracking()
            .AnyAsync(r =>
                r.NetworkId == effectiveNetworkId.Value &&
                r.FeatureKey == pricing.FeatureKey &&
                r.Status == NetworkServiceRequestStatus.Pending);

        if (hasPending)
        {
            TempData["Info"] = "يوجد طلب معلّق لهذه الخدمة بانتظار موافقة مدير النظام.";
            return RedirectToAction(nameof(Index), new { highlight = pricing.FeatureKey });
        }

        List<int> scopeNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(context, effectiveNetworkId.Value);
        int multiplier = pricing.ChargeUnit == PricingChargeUnit.Flat
            ? 1
            : await PricingChargeHelper.GetMultiplierAsync(context, scopeNetworkIds, pricing.ChargeUnit);
        decimal chargeAmount = WalletMath.CeilSyp(pricing.AmountSYP * multiplier);

        try
        {
            await using IDbContextTransaction tx = await context.Database.BeginTransactionAsync();

            Network? company = await context.Networks
                .FirstOrDefaultAsync(n => n.Id == effectiveNetworkId.Value && n.ParentNetworkId == null)
                ?? await context.Networks.FirstOrDefaultAsync(n => n.Id == effectiveNetworkId.Value);

            if (company == null)
            {
                TempData["Error"] = "تعذر تحديد حساب الشركة.";
                return RedirectToAction(nameof(Index));
            }

            if (chargeAmount > 0m && company.Balance < chargeAmount)
            {
                TempData["Error"] = AppMessages.InsufficientBalance;
                return RedirectToAction(nameof(Index), new { highlight = pricing.FeatureKey });
            }

            int? chargeWalletTransactionId = null;
            if (chargeAmount > 0m)
            {
                decimal previousBalance = company.Balance;
                company.Balance -= chargeAmount;

                NetworkWalletTransaction walletTx = new()
                {
                    NetworkId = effectiveNetworkId.Value,
                    Type = NetworkWalletTransactionType.ServiceCharge,
                    SignedAmount = -chargeAmount,
                    PreviousBalance = previousBalance,
                    NewBalance = company.Balance,
                    CreatedByUserId = user.Id,
                    CreatedAt = now,
                    Notes = $"طلب اشتراك خدمة: {pricing.FeatureKey} / {pricing.BillingPeriod} (تسعير #{pricing.Id})"
                };
                context.NetworkWalletTransactions.Add(walletTx);
                await context.SaveChangesAsync();
                chargeWalletTransactionId = walletTx.Id;
            }

            NetworkServiceRequest request = new()
            {
                NetworkId = effectiveNetworkId.Value,
                FeatureKey = pricing.FeatureKey,
                FeaturePricingId = pricing.Id,
                BillingPeriod = pricing.BillingPeriod,
                AmountSYP = chargeAmount,
                AmountUSD = pricing.AmountUSD,
                Currency = pricing.Currency,
                Status = NetworkServiceRequestStatus.Pending,
                RequestedByUserId = user.Id,
                RequestedAt = now,
                ChargeWalletTransactionId = chargeWalletTransactionId,
                Notes = chargeAmount > 0m
                    ? $"خصم مقدّم {chargeAmount:N0} ل.س.ج بانتظار الموافقة."
                    : "طلب تفعيل بدون خصم مقدّم."
            };
            context.NetworkServiceRequests.Add(request);
            await context.SaveChangesAsync();
            await tx.CommitAsync();

            TempData["Success"] = chargeAmount > 0m
                ? $"تم إرسال طلب تفعيل الخدمة وخصم {chargeAmount:N0} ل.س.ج من المحفظة بانتظار موافقة مدير النظام."
                : "تم إرسال طلب تفعيل الخدمة بانتظار موافقة مدير النظام.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to submit service subscription request for pricing #{PricingId}", pricingId);
            TempData["Error"] = "تعذر إرسال طلب الخدمة.";
        }

        return RedirectToAction(nameof(Index), new { highlight = pricing.FeatureKey });
    }

    private async Task<List<CompanyServiceItemViewModel>> BuildServicesListAsync(int effectiveNetworkId)
    {
        DateTime now = DateTime.Now;

        Dictionary<string, FeaturePublicInfo> publicInfoByKey = await context.FeaturePublicInfos
            .AsNoTracking()
            .ToDictionaryAsync(f => f.FeatureKey, f => f, StringComparer.OrdinalIgnoreCase);

        List<SystemService> customServices = await context.SystemServices
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.DisplayName)
            .ToListAsync();

        List<(string Key, string DisplayName, string Category, string Description)> allServiceDefs = FeatureCatalog.All
            .OrderBy(f => f.Category)
            .ThenBy(f => f.DisplayName)
            .Select(def => (def.Key, def.DisplayName, def.Category, def.Description))
            .Concat(customServices.Select(s => (s.Key, s.DisplayName, "خدمات مخصصة", s.Description ?? "")))
            .ToList();

        List<string> subscribablePricingKeys = await context.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.ChargeUnit != PricingChargeUnit.PercentOfCollectedAmount)
            .Select(p => p.FeatureKey)
            .Distinct()
            .ToListAsync();

        foreach (string pricingKey in subscribablePricingKeys.Where(k => !NonSubscribableFeatureKeys.Contains(k)))
        {
            AppendServiceDefinitionIfMissing(allServiceDefs, pricingKey);
        }

        foreach (string documentedKey in publicInfoByKey.Keys)
        {
            AppendServiceDefinitionIfMissing(allServiceDefs, documentedKey);
        }

        List<string> featureKeys = allServiceDefs.Select(d => d.Key).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        HashSet<string> enabledFeatureKeys = (await context.NetworkFeatures
            .AsNoTracking()
            .Where(f => f.NetworkId == effectiveNetworkId && f.IsEnabled)
            .Select(f => f.Key)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<NetworkServiceSubscription> activeSubs = await context.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s =>
                s.NetworkId == effectiveNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Active &&
                s.ExpiresAt > now &&
                featureKeys.Contains(s.FeatureKey))
            .ToListAsync();

        Dictionary<string, NetworkServiceSubscription> activeSubsByKey = activeSubs
            .GroupBy(s => s.FeatureKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.ExpiresAt).First(),
                StringComparer.OrdinalIgnoreCase);

        List<NetworkServiceSubscription> allSubsForKeys = await context.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s => s.NetworkId == effectiveNetworkId && featureKeys.Contains(s.FeatureKey))
            .ToListAsync();

        Dictionary<string, NetworkServiceSubscription> latestSubsByKey = allSubsForKeys
            .GroupBy(s => s.FeatureKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.ExpiresAt).First(),
                StringComparer.OrdinalIgnoreCase);

        List<NetworkServiceRequest> pendingReqs = await context.NetworkServiceRequests
            .AsNoTracking()
            .Where(r =>
                r.NetworkId == effectiveNetworkId &&
                r.Status == NetworkServiceRequestStatus.Pending &&
                featureKeys.Contains(r.FeatureKey))
            .ToListAsync();

        Dictionary<string, NetworkServiceRequest> pendingByKey = pendingReqs
            .GroupBy(r => r.FeatureKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.RequestedAt).First(),
                StringComparer.OrdinalIgnoreCase);

        List<FeaturePricing> activePricings = await context.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                featureKeys.Contains(p.FeatureKey) &&
                p.ChargeUnit != PricingChargeUnit.PercentOfCollectedAmount)
            .OrderBy(p => p.BillingPeriod)
            .ThenBy(p => p.Id)
            .ToListAsync();

        activePricings = activePricings
            .Where(p => !NonSubscribableFeatureKeys.Contains(p.FeatureKey))
            .ToList();

        List<int> scopeNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(context, effectiveNetworkId);

        List<CompanyServiceItemViewModel> servicesList = [];
        foreach ((string key, string displayName, string category, string description) in allServiceDefs)
        {
            publicInfoByKey.TryGetValue(key, out FeaturePublicInfo? pubInfo);
            activeSubsByKey.TryGetValue(key, out NetworkServiceSubscription? activeSub);
            pendingByKey.TryGetValue(key, out NetworkServiceRequest? pendingReq);

            bool hasActive = activeSub != null || enabledFeatureKeys.Contains(key);
            bool hasPending = pendingReq != null;
            latestSubsByKey.TryGetValue(key, out NetworkServiceSubscription? latestSub);

            string detailHtml = !string.IsNullOrWhiteSpace(pubInfo?.DetailHtml)
                ? pubInfo!.DetailHtml!
                : BuildDefaultDetailHtml(displayName, description);

            string pricingPolicyHtml = !string.IsNullOrWhiteSpace(pubInfo?.PricingPolicyHtml)
                ? pubInfo!.PricingPolicyHtml!
                : ServiceCatalogDocumentationHelper.BuildDefaultPricingPolicyHtml(displayName);

            string? renewalPolicyHtml = !string.IsNullOrWhiteSpace(pubInfo?.RenewalPolicyHtml)
                ? pubInfo!.RenewalPolicyHtml
                : ServiceCatalogDocumentationHelper.BuildDefaultRenewalPolicyHtml(displayName);

            List<CompanyServicePricingOptionViewModel> pricingOptions = [];
            foreach (FeaturePricing pricing in activePricings.Where(p => string.Equals(p.FeatureKey, key, StringComparison.OrdinalIgnoreCase)))
            {
                int multiplier = pricing.ChargeUnit == PricingChargeUnit.Flat
                    ? 1
                    : await PricingChargeHelper.GetMultiplierAsync(context, scopeNetworkIds, pricing.ChargeUnit);
                pricingOptions.Add(new()
                {
                    PricingId = pricing.Id,
                    BillingPeriod = pricing.BillingPeriod,
                    ChargeUnit = pricing.ChargeUnit,
                    AmountSYP = pricing.AmountSYP,
                    AmountUSD = pricing.AmountUSD,
                    Currency = pricing.Currency,
                    IsActive = pricing.IsActive,
                    EstimatedChargeSyp = WalletMath.CeilSyp(pricing.AmountSYP * multiplier)
                });
            }

            string? inactiveReason = null;
            if (!hasActive && !hasPending)
            {
                inactiveReason = ResolveInactiveReason(key, latestSub, pendingReq, pricingOptions);
            }

            servicesList.Add(new()
            {
                FeatureKey = key,
                DisplayName = displayName,
                Category = category,
                Description = description ?? "",
                DetailHtml = detailHtml,
                PricingPolicyHtml = pricingPolicyHtml,
                RenewalPolicyHtml = renewalPolicyHtml,
                HasActiveSubscription = hasActive,
                ExpiresAt = activeSub?.ExpiresAt,
                StartAt = activeSub?.StartAt,
                InactiveReason = inactiveReason,
                HasPendingRequest = hasPending,
                PendingRequestId = pendingReq?.Id,
                PricingOptions = pricingOptions
            });
        }

        return servicesList
            .OrderBy(s => s.Category)
            .ThenBy(s => s.DisplayName)
            .ToList();
    }

    private static string? ResolveInactiveReason(
        string featureKey,
        NetworkServiceSubscription? subscription,
        NetworkServiceRequest? pendingRequest,
        List<CompanyServicePricingOptionViewModel> pricingOptions)
    {
        if (NonSubscribableFeatureKeys.Contains(featureKey))
        {
            return "خدمة إعداد نظام (عمولة/ضريبة) — لا تُفعّل كاشتراك لمدير الشركة.";
        }

        if (pendingRequest != null)
        {
            return null;
        }

        if (subscription != null)
        {
            if (subscription.Status != NetworkServiceSubscriptionStatus.Active)
            {
                return $"الاشتراك بحالة «{subscription.Status}» — يحتاج تجديداً أو موافقة.";
            }

            if (subscription.ExpiresAt <= DateTime.Now)
            {
                return $"انتهى الاشتراك في {subscription.ExpiresAt:yyyy/MM/dd} — يُجدَّد تلقائياً عند إعادة تحميل الصفحة.";
            }
        }

        if (pricingOptions.Count == 0)
        {
            return "لا يوجد تسعير نشط من مدير النظام لهذه الخدمة.";
        }

        return "لا يوجد اشتراك فعّال مسجّل — تمت محاولة التفعيل التلقائي؛ أعد تحميل الصفحة.";
    }

    private static void AppendServiceDefinitionIfMissing(
        List<(string Key, string DisplayName, string Category, string Description)> defs,
        string featureKey)
    {
        if (string.IsNullOrWhiteSpace(featureKey) ||
            defs.Exists(d => string.Equals(d.Key, featureKey, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        FeatureCatalog.FeatureDefinition? catalogDef = FeatureCatalog.All
            .FirstOrDefault(f => string.Equals(f.Key, featureKey, StringComparison.OrdinalIgnoreCase));

        defs.Add((
            featureKey,
            catalogDef?.DisplayName ?? featureKey,
            catalogDef?.Category ?? "خدمات أخرى",
            catalogDef?.Description ?? ""));
    }

    private static string BuildDefaultDetailHtml(string displayName, string? description)
    {
        string safeName = WebUtility.HtmlEncode(displayName ?? "الخدمة");
        string safeDescription = WebUtility.HtmlEncode((description ?? string.Empty).Trim());
        string descriptionHtml = string.IsNullOrWhiteSpace(safeDescription)
            ? "اطلب تفعيل الخدمة من الخطط المتاحة بعد مراجعة التسعير."
            : safeDescription;

        return $"""
                <p><strong>{safeName}</strong></p>
                <p>{descriptionHtml}</p>
                <p class="text-muted mb-0">بعد الموافقة تظهر روابط الخدمة في القائمة الجانبية لمدير الشركة والموظفين المصرّح لهم.</p>
                """;
    }
}
