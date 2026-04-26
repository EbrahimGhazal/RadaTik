using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;

namespace RadTik.Services;

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
        var now = DateTime.Now;

        var overdueSuspendedIds = await _db.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s =>
                s.NetworkId == companyNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Suspended &&
                s.BillingPeriod != PricingBillingPeriod.OneTime &&
                s.ExpiresAt <= now)
            .OrderBy(s => s.ExpiresAt)
            .Select(s => s.Id)
            .ToListAsync(ct);

        foreach (var subId in overdueSuspendedIds)
        {
            await _renewalProcessor.ProcessSubscriptionRenewalAsync(_db, subId, now, ct);
        }

        var suspendedFuture = await _db.NetworkServiceSubscriptions
            .Where(s =>
                s.NetworkId == companyNetworkId &&
                s.Status == NetworkServiceSubscriptionStatus.Suspended &&
                s.ExpiresAt > now &&
                s.BillingPeriod != PricingBillingPeriod.OneTime)
            .ToListAsync(ct);

        foreach (var sub in suspendedFuture)
        {
            sub.Status = NetworkServiceSubscriptionStatus.Active;
            sub.UpdatedAt = now;
        }

        if (suspendedFuture.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        var hasActive = await _db.NetworkServiceSubscriptions
            .AnyAsync(s => s.NetworkId == companyNetworkId && s.Status == NetworkServiceSubscriptionStatus.Active, ct);

        if (!hasActive)
        {
            return;
        }

        try
        {
            var actorId = await _usageCharge.ResolveActorUserIdAsync(companyNetworkId, ct);
            await _usageCharge.ChargeUsageIncreaseAsync(companyNetworkId, actorId, null, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Usage charge retry after top-up failed for company {CompanyId}", companyNetworkId);
        }
    }
}
