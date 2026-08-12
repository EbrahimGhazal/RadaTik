using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;

namespace RadaTik.Helpers;

/// <summary>
/// عند إنشاء شبكة الشركة الرئيسية لأول مرة: تفعيل جميع الخدمات ذات التسعير النشط في النظام،
/// مع اختيار سيناريو تسعير واحد لكل خدمة (صف واحد من FeaturePricings) ليتوافق مع ربط الاشتراك بـ BillingPeriod.
/// </summary>
public static class CompanySubscriptionBootstrap
{
    private static readonly HashSet<string> AutoSubscribeExcludedFeatureKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        FeatureKeys.CollectionCommission,
        FeatureKeys.ReportsExport,
        FeatureKeys.MaintenanceCommission,
        FeatureKeys.MaintenanceTransportFee,
        // ضريبة سعر البروفايل خدمة مرجعية لتسعير العميل فقط، وليست خدمة اشتراك تُخصم من مدير الشركة.
        FeatureKeys.ProfilePriceTax
    };

    private static int BillingPeriodPreferenceRank(PricingBillingPeriod period) => period switch
    {
        PricingBillingPeriod.Monthly => 0,
        PricingBillingPeriod.Every3Months => 1,
        PricingBillingPeriod.Every6Months => 2,
        PricingBillingPeriod.Every12Months => 3,
        PricingBillingPeriod.Daily => 4,
        PricingBillingPeriod.OneTime => 5,
        _ => 99
    };

    /// <summary>
    /// يُستدعى عند إنشاء شبكة الشركة الرئيسية أو عند بذر الشركات الحالية: تفعيل كل الخدمات.
    /// </summary>
    public static async Task EnsureFullCompanyEntitlementsAsync(
        ApplicationDbContext context,
        IUsageBasedSubscriptionChargeService? usageChargeService,
        int companyNetworkId,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        Network? company = await context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, cancellationToken);
        if (company == null)
        {
            return;
        }

        await EnableAllNetworkFeaturesAsync(context, companyNetworkId, cancellationToken);
        await EnsureActiveSubscriptionsAsync(context, usageChargeService, companyNetworkId, logger, cancellationToken);
    }

    /// <summary>
    /// يُستدعى مرة واحدة عند إنشاء شبكة الشركة الرئيسية (ParentNetworkId == null).
    /// </summary>
    public static Task SeedActiveSubscriptionsForNewMainCompanyNetworkAsync(
        ApplicationDbContext context,
        IUsageBasedSubscriptionChargeService usageChargeService,
        int companyNetworkId,
        ILogger? logger,
        CancellationToken cancellationToken = default) =>
        EnsureFullCompanyEntitlementsAsync(context, usageChargeService, companyNetworkId, logger, cancellationToken);

    private static async Task EnableAllNetworkFeaturesAsync(
        ApplicationDbContext context,
        int companyNetworkId,
        CancellationToken cancellationToken)
    {
        List<string> allKeys = await GetAllCompanyFeatureKeysAsync(context, cancellationToken);
        DateTime now = DateTime.Now;

        List<NetworkFeature> existing = await context.NetworkFeatures
            .Where(f => f.NetworkId == companyNetworkId)
            .ToListAsync(cancellationToken);

        Dictionary<string, NetworkFeature> byKey = existing.ToDictionary(f => f.Key, StringComparer.OrdinalIgnoreCase);

        foreach (string key in allKeys)
        {
            if (byKey.TryGetValue(key, out NetworkFeature? feature))
            {
                if (!feature.IsEnabled)
                {
                    feature.IsEnabled = true;
                    feature.UpdatedAt = now;
                }

                continue;
            }

            context.NetworkFeatures.Add(new()
            {
                NetworkId = companyNetworkId,
                Key = key,
                IsEnabled = true,
                CreatedAt = now,
                UpdatedAt = now
            });
        }

        await context.SaveChangesAsync(cancellationToken);
    }

    private static async Task<List<string>> GetAllCompanyFeatureKeysAsync(
        ApplicationDbContext context,
        CancellationToken cancellationToken)
    {
        List<string> keys = FeatureCatalog.All.Select(f => f.Key).ToList();

        List<string> customKeys = await context.SystemServices
            .AsNoTracking()
            .Where(s => s.IsActive)
            .Select(s => s.Key)
            .ToListAsync(cancellationToken);

        keys.AddRange(customKeys);
        return keys.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static async Task EnsureActiveSubscriptionsAsync(
        ApplicationDbContext context,
        IUsageBasedSubscriptionChargeService? usageChargeService,
        int companyNetworkId,
        ILogger? logger,
        CancellationToken cancellationToken)
    {
        List<FeaturePricing> activePricings = await context.FeaturePricings.AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        Dictionary<string, FeaturePricing> chosenByKey = new(StringComparer.OrdinalIgnoreCase);
        foreach (IGrouping<string, FeaturePricing> g in activePricings.GroupBy(p => p.FeatureKey, StringComparer.OrdinalIgnoreCase))
        {
            string key = g.Key;
            if (string.IsNullOrWhiteSpace(key) || AutoSubscribeExcludedFeatureKeys.Contains(key))
            {
                continue;
            }

            List<FeaturePricing> candidates = g
                .Where(p => p.ChargeUnit != PricingChargeUnit.PercentOfCollectedAmount)
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            FeaturePricing best = candidates
                .OrderBy(p => BillingPeriodPreferenceRank(p.BillingPeriod))
                .ThenBy(p => p.Id)
                .First();

            chosenByKey[key] = best;
        }

        DateTime now = DateTime.Now;
        DateTime managerGrantExpiresAt = now.AddYears(10);

        List<NetworkServiceSubscription> existingSubs = await context.NetworkServiceSubscriptions
            .Where(s => s.NetworkId == companyNetworkId)
            .ToListAsync(cancellationToken);

        Dictionary<string, NetworkServiceSubscription> subsByKey = existingSubs
            .GroupBy(s => s.FeatureKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.ExpiresAt).First(),
                StringComparer.OrdinalIgnoreCase);

        List<string> keysToEnsure = (await GetAllCompanyFeatureKeysAsync(context, cancellationToken))
            .Concat(chosenByKey.Keys)
            .Where(k => !string.IsNullOrWhiteSpace(k) && !AutoSubscribeExcludedFeatureKeys.Contains(k))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        List<NetworkServiceSubscription> newSubs = [];
        bool subscriptionsChanged = false;
        foreach (string featureKey in keysToEnsure)
        {
            chosenByKey.TryGetValue(featureKey, out FeaturePricing? pricing);
            PricingBillingPeriod billingPeriod = pricing?.BillingPeriod ?? PricingBillingPeriod.Monthly;

            if (subsByKey.TryGetValue(featureKey, out NetworkServiceSubscription? existing))
            {
                bool rowChanged = false;
                if (existing.Status != NetworkServiceSubscriptionStatus.Active)
                {
                    existing.Status = NetworkServiceSubscriptionStatus.Active;
                    rowChanged = true;
                }

                if (existing.ExpiresAt <= now)
                {
                    existing.ExpiresAt = managerGrantExpiresAt;
                    rowChanged = true;
                }

                if (rowChanged)
                {
                    existing.UpdatedAt = now;
                    subscriptionsChanged = true;
                }

                continue;
            }

            DateTime expiresAt = pricing == null
                ? managerGrantExpiresAt
                : BillingPeriodDateCalculator.AddPeriod(now, billingPeriod);
            if (expiresAt <= now)
            {
                expiresAt = managerGrantExpiresAt;
            }

            NetworkServiceSubscription sub = new()
            {
                NetworkId = companyNetworkId,
                FeatureKey = featureKey,
                BillingPeriod = billingPeriod,
                StartAt = now,
                ExpiresAt = expiresAt,
                Status = NetworkServiceSubscriptionStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                LastApprovedRequestId = null
            };
            newSubs.Add(sub);
            subsByKey[featureKey] = sub;
        }

        if (newSubs.Count > 0)
        {
            context.NetworkServiceSubscriptions.AddRange(newSubs);
            subscriptionsChanged = true;
        }

        if (subscriptionsChanged)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        if (usageChargeService == null)
        {
            return;
        }

        foreach (NetworkServiceSubscription sub in newSubs)
        {
            try
            {
                await usageChargeService.InitializeBaselineAsync(companyNetworkId, sub.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex,
                    "تعذر تهيئة سجل الاستخدام للاشتراك {SubscriptionId} للشبكة {NetworkId}",
                    sub.Id, companyNetworkId);
            }
        }
    }
}
