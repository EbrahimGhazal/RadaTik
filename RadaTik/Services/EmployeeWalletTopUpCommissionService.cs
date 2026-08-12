using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services;

public sealed class EmployeeWalletTransferPricingResult
{
    public decimal TopUpAmount { get; init; }
    public decimal CommissionSyp { get; init; }
    public decimal TotalCompanyDebit => TopUpAmount + CommissionSyp;
    public bool HasCommission => CommissionSyp > 0m;
    public decimal? CommissionPercent { get; init; }
    public bool SkippedNoPricing { get; init; }
}

public sealed class EmployeeWalletTopUpCommissionService(
    ApplicationDbContext context,
    ICollectionCommissionPricingResolver commissionResolver)
{
    public async Task<EmployeeWalletTransferPricingResult> CalculateAsync(
        decimal topUpAmount,
        CancellationToken cancellationToken = default)
    {
        if (topUpAmount <= 0m)
        {
            return new() { TopUpAmount = topUpAmount };
        }

        FeaturePricing? pricing = await context.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == FeatureKeys.PayrollWalletTransferCommission)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (pricing == null || pricing.AmountSYP <= 0m)
        {
            return new()
            {
                TopUpAmount = topUpAmount,
                SkippedNoPricing = true
            };
        }

        if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
        {
            CollectionCommissionPricingComputation computation = commissionResolver.Resolve(pricing, topUpAmount);
            if (!computation.IsSupported)
            {
                return new()
                {
                    TopUpAmount = topUpAmount,
                    SkippedNoPricing = true
                };
            }

            return new()
            {
                TopUpAmount = topUpAmount,
                CommissionSyp = computation.FeeAmountSyp,
                CommissionPercent = computation.PercentValue
            };
        }

        if (pricing.ChargeUnit == PricingChargeUnit.PerRequest)
        {
            return new()
            {
                TopUpAmount = topUpAmount,
                CommissionSyp = WalletMath.CeilSyp(pricing.AmountSYP)
            };
        }

        return new()
        {
            TopUpAmount = topUpAmount,
            SkippedNoPricing = true
        };
    }
}
