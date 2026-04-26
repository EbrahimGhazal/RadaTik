using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services.SystemAdminPricing;

namespace RadTik.Services;

/// <summary>
/// منطق تجديد دورة اشتراك واحد (خصم + تمديد ExpiresAt). يُستعمل من المهمة الخلفية وبعد شحن المحفظة.
/// </summary>
public sealed class NetworkSubscriptionRenewalProcessor
{
    private readonly ILogger<NetworkSubscriptionRenewalProcessor> _logger;

    public NetworkSubscriptionRenewalProcessor(ILogger<NetworkSubscriptionRenewalProcessor> logger)
    {
        _logger = logger;
    }

    public async Task ProcessSubscriptionRenewalAsync(ApplicationDbContext db, int subscriptionId, DateTime now, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var sub = await db.NetworkServiceSubscriptions.FirstOrDefaultAsync(s => s.Id == subscriptionId, ct);
            if (sub == null)
            {
                return;
            }

            if (sub.BillingPeriod == PricingBillingPeriod.OneTime)
            {
                return;
            }

            var pricing = await db.FeaturePricings
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.FeatureKey == sub.FeatureKey &&
                    p.BillingPeriod == sub.BillingPeriod, ct);

            if (pricing == null)
            {
                sub.Status = NetworkServiceSubscriptionStatus.Suspended;
                sub.UpdatedAt = now;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return;
            }

            if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
            {
                await tx.RollbackAsync(ct);
                return;
            }

            var initialPricing = await db.FeaturePricings
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.FeatureKey == sub.FeatureKey &&
                    p.ChargeUnit == pricing.ChargeUnit &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime, ct);
            var policy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, pricing);

            var company = await db.Networks.FirstOrDefaultAsync(n => n.Id == sub.NetworkId && n.ParentNetworkId == null, ct);
            if (company == null)
            {
                await tx.RollbackAsync(ct);
                return;
            }

            var actorUserId = await ResolveActorUserIdAsync(db, company, ct);
            var networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(db, sub.NetworkId);

            for (var i = 0; i < 12 && sub.ExpiresAt <= now; i++)
            {
                var cycleEnd = sub.ExpiresAt;
                var cycleStart = SubtractPeriod(cycleEnd, sub.BillingPeriod);

                var multiplier = pricing.ChargeUnit == PricingChargeUnit.PerRequest
                    ? await PricingChargeHelper.GetMultiplierAsync(db, networkIds, pricing.ChargeUnit, cycleStart, cycleEnd)
                    : await PricingChargeHelper.GetMultiplierAsync(db, networkIds, pricing.ChargeUnit);

                // Apply configurable free renewal units before renewal charge starts.
                multiplier = Math.Max(0, multiplier - policy.FreeRenewalUnits);

                var chargeAmount = WalletMath.CeilSyp(pricing.AmountSYP * multiplier);
                if (chargeAmount < 0)
                {
                    sub.Status = NetworkServiceSubscriptionStatus.Suspended;
                    sub.UpdatedAt = now;
                    break;
                }

                if (chargeAmount > 0 && company.Balance < chargeAmount)
                {
                    sub.Status = NetworkServiceSubscriptionStatus.Suspended;
                    sub.UpdatedAt = now;
                    break;
                }

                var previousBalance = company.Balance;
                company.Balance = previousBalance - chargeAmount;

                if (chargeAmount > 0)
                {
                    db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                    {
                        NetworkId = sub.NetworkId,
                        Type = NetworkWalletTransactionType.ServiceCharge,
                        SignedAmount = -chargeAmount,
                        PreviousBalance = previousBalance,
                        NewBalance = company.Balance,
                        NetworkServiceSubscriptionId = sub.Id,
                        CreatedByUserId = actorUserId,
                        CreatedAt = now,
                        Notes = pricing.ChargeUnit == PricingChargeUnit.Flat
                            ? $"تجديد تلقائي: {sub.FeatureKey} / {sub.BillingPeriod} (حتى {cycleEnd:yyyy/MM/dd})"
                            : $"تجديد تلقائي: {sub.FeatureKey} / {sub.BillingPeriod} / {pricing.ChargeUnit} × {multiplier} (حتى {cycleEnd:yyyy/MM/dd})"
                    });
                }

                sub.ExpiresAt = AddPeriod(sub.ExpiresAt, sub.BillingPeriod);
                sub.Status = NetworkServiceSubscriptionStatus.Active;
                sub.UpdatedAt = now;
            }

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync(ct);
            _logger.LogWarning(ex, "Failed billing for subscription #{SubscriptionId}", subscriptionId);
        }
    }

    private static DateTime AddPeriod(DateTime dt, PricingBillingPeriod p) => p switch
    {
        PricingBillingPeriod.Daily => dt.AddDays(1),
        PricingBillingPeriod.Monthly => dt.AddMonths(1),
        PricingBillingPeriod.Every3Months => dt.AddMonths(3),
        PricingBillingPeriod.Every6Months => dt.AddMonths(6),
        PricingBillingPeriod.Every12Months => dt.AddYears(1),
        _ => dt
    };

    private static DateTime SubtractPeriod(DateTime dt, PricingBillingPeriod p) => p switch
    {
        PricingBillingPeriod.Daily => dt.AddDays(-1),
        PricingBillingPeriod.Monthly => dt.AddMonths(-1),
        PricingBillingPeriod.Every3Months => dt.AddMonths(-3),
        PricingBillingPeriod.Every6Months => dt.AddMonths(-6),
        PricingBillingPeriod.Every12Months => dt.AddYears(-1),
        _ => dt
    };

    private static async Task<string> ResolveActorUserIdAsync(ApplicationDbContext db, Network company, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(company.ManagerUserId))
        {
            return company.ManagerUserId!;
        }

        var sysAdminId = await (from u in db.Users
            join ur in db.UserRoles on u.Id equals ur.UserId
            join r in db.Roles on ur.RoleId equals r.Id
            where r.Name == RoleNames.SystemAdministrator
            select u.Id).FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(sysAdminId))
        {
            return sysAdminId!;
        }

        return await db.Users.Select(u => u.Id).FirstAsync(ct);
    }
}
