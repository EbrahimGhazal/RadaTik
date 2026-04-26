using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Helpers;

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
    /// يُستدعى مرة واحدة عند إنشاء شبكة الشركة الرئيسية (ParentNetworkId == null).
    /// </summary>
    public static async Task SeedActiveSubscriptionsForNewMainCompanyNetworkAsync(
        ApplicationDbContext context,
        IUsageBasedSubscriptionChargeService usageChargeService,
        int companyNetworkId,
        ILogger? logger,
        CancellationToken cancellationToken = default)
    {
        var company = await context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, cancellationToken);
        if (company == null)
        {
            return;
        }

        var activePricings = await context.FeaturePricings.AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(cancellationToken);

        var chosenByKey = new Dictionary<string, FeaturePricing>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in activePricings.GroupBy(p => p.FeatureKey, StringComparer.OrdinalIgnoreCase))
        {
            var key = g.Key;
            if (string.IsNullOrWhiteSpace(key) || AutoSubscribeExcludedFeatureKeys.Contains(key))
            {
                continue;
            }

            var candidates = g
                .Where(p => p.ChargeUnit != PricingChargeUnit.PercentOfCollectedAmount)
                .ToList();
            if (candidates.Count == 0)
            {
                continue;
            }

            var best = candidates
                .OrderBy(p => BillingPeriodPreferenceRank(p.BillingPeriod))
                .ThenBy(p => p.Id)
                .First();

            chosenByKey[key] = best;
        }

        var now = DateTime.Now;
        var existingKeys = await context.NetworkServiceSubscriptions.AsNoTracking()
            .Where(s => s.NetworkId == companyNetworkId)
            .Select(s => s.FeatureKey)
            .ToListAsync(cancellationToken);
        var existingSet = existingKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var newSubs = new List<NetworkServiceSubscription>();
        foreach (var (featureKey, pricing) in chosenByKey)
        {
            if (existingSet.Contains(featureKey))
            {
                continue;
            }

            newSubs.Add(new NetworkServiceSubscription
            {
                NetworkId = companyNetworkId,
                FeatureKey = featureKey,
                BillingPeriod = pricing.BillingPeriod,
                StartAt = now,
                ExpiresAt = BillingPeriodDateCalculator.AddPeriod(now, pricing.BillingPeriod),
                Status = NetworkServiceSubscriptionStatus.Active,
                CreatedAt = now,
                UpdatedAt = now,
                LastApprovedRequestId = null
            });
        }

        if (newSubs.Count > 0)
        {
            context.NetworkServiceSubscriptions.AddRange(newSubs);
            await context.SaveChangesAsync(cancellationToken);
        }

        foreach (var sub in newSubs)
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
