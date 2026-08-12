using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Helpers;

/// <summary>
/// يحدد ما إذا كانت الشركة (الشبكة الرئيسية) تملك حق استخدام ميزة عبر اشتراك فعّال أو الجدول القديم.
/// </summary>
public static class CompanyServiceEntitlementResolver
{
    public static async Task<int?> ResolveEffectiveCompanyNetworkIdAsync(
        ApplicationDbContext context,
        int? selectedNetworkId,
        CancellationToken cancellationToken = default)
    {
        if (!selectedNetworkId.HasValue)
        {
            return null;
        }

        Network? selected = await context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value, cancellationToken);

        if (selected == null)
        {
            return null;
        }

        return selected.ParentNetworkId ?? selected.Id;
    }

    public static async Task<bool> HasEntitlementAsync(
        ApplicationDbContext context,
        int effectiveCompanyNetworkId,
        string featureKey,
        DateTime? asOf = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(featureKey))
        {
            return false;
        }

        DateTime now = asOf ?? DateTime.Now;

        bool hasActiveSubscription = await context.NetworkServiceSubscriptions
            .AsNoTracking()
            .AnyAsync(s =>
                s.NetworkId == effectiveCompanyNetworkId &&
                s.FeatureKey == featureKey &&
                s.Status == NetworkServiceSubscriptionStatus.Active &&
                s.ExpiresAt > now,
                cancellationToken);

        if (hasActiveSubscription)
        {
            return true;
        }

        return await context.NetworkFeatures
            .AsNoTracking()
            .AnyAsync(f =>
                f.NetworkId == effectiveCompanyNetworkId &&
                f.Key == featureKey &&
                f.IsEnabled,
                cancellationToken);
    }
}
