using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Security;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services;

/// <summary>
/// بعد شحن محفظة الشركة: إعادة محاولة تجديد الدورات المستحقة وإعادة تفعيل الاشتراكات المعلّقة لأسباب الرصيد ثم مزامنة خصم الوحدات.
/// </summary>
public interface IWalletTopUpSubscriptionResumeService
{
    Task ResumeAfterCompanyWalletTopUpAsync(int companyNetworkId, CancellationToken ct = default);
}

public sealed class WalletTopUpSubscriptionResumeService : IWalletTopUpSubscriptionResumeService
{
    private readonly ApplicationDbContext _db;
    private readonly NetworkSubscriptionRenewalProcessor _renewalProcessor;
    private readonly IUsageBasedSubscriptionChargeService _usageCharge;
    private readonly ILogger<WalletTopUpSubscriptionResumeService> _logger;

    public WalletTopUpSubscriptionResumeService(
        ApplicationDbContext db,
        NetworkSubscriptionRenewalProcessor renewalProcessor,
        IUsageBasedSubscriptionChargeService usageCharge,
        ILogger<WalletTopUpSubscriptionResumeService> logger)
    {
        _db = db;
        _renewalProcessor = renewalProcessor;
        _usageCharge = usageCharge;
        _logger = logger;
    }

    public async Task ResumeAfterCompanyWalletTopUpAsync(int companyNetworkId, CancellationToken ct = default)
    {
        DateTime now = DateTime.Now;

        try
        {
            FeaturePricing? initialNetworkPricing = await _db.FeaturePricings.AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.Networks &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime &&
                    p.ChargeUnit == PricingChargeUnit.PerNetwork, ct);

            if (initialNetworkPricing != null)
            {
                Network? company = await _db.Networks.AsNoTracking()
                    .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
                if (company != null)
                {
                    string actorId = await _usageCharge.ResolveActorUserIdAsync(companyNetworkId, ct);
                    bool charged = await MainNetworkCreationBilling.TryApplyOneTimeCreationChargeAsync(
                        _db,
                        companyNetworkId,
                        company.Name,
                        FeatureKeys.Networks,
                        initialNetworkPricing.AmountSYP,
                        actorId,
                        ct);

                    // حتى لو كان الخصم مطبّقاً مسبقاً: ثبّت الـ Ledger حتى لا يُخصم مجدداً كعنصر جديد.
                    if (!charged)
                    {
                        await MainNetworkCreationBilling.EnsureMainNetworkUnitLedgerAsync(
                            _db,
                            companyNetworkId,
                            FeatureKeys.Networks,
                            DateTime.Now,
                            charged: true,
                            ct);
                        await _db.SaveChangesAsync(ct);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Deferred main-network creation charge failed for company {CompanyId}",
                companyNetworkId);
        }

        List<int> overdueSuspendedIds = await _db.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s =>
                s.NetworkId == companyNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Suspended &&
                s.BillingPeriod != PricingBillingPeriod.OneTime &&
                s.ExpiresAt <= now)
            .OrderBy(s => s.ExpiresAt)
            .Select(s => s.Id)
            .ToListAsync(ct);

        foreach (int subId in overdueSuspendedIds)
        {
            await _renewalProcessor.ProcessSubscriptionRenewalAsync(_db, subId, now, ct);
        }

        List<NetworkServiceSubscription> suspendedFuture = await _db.NetworkServiceSubscriptions
            .Where(s =>
                s.NetworkId == companyNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Suspended &&
                s.ExpiresAt > now &&
                s.BillingPeriod != PricingBillingPeriod.OneTime)
            .ToListAsync(ct);

        foreach (NetworkServiceSubscription? sub in suspendedFuture)
        {
            sub.Status = NetworkServiceSubscriptionStatus.Active;
            sub.UpdatedAt = now;
        }

        if (suspendedFuture.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        bool hasActive = await _db.NetworkServiceSubscriptions
            .AnyAsync(s => s.NetworkId == companyNetworkId && s.Status == NetworkServiceSubscriptionStatus.Active, ct);

        if (!hasActive)
        {
            return;
        }

        try
        {
            string actorId = await _usageCharge.ResolveActorUserIdAsync(companyNetworkId, ct);
            await _usageCharge.ChargeUsageIncreaseAsync(companyNetworkId, actorId, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Usage charge retry after top-up failed for company {CompanyId}", companyNetworkId);
        }
    }
}
