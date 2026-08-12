using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services;

public sealed class CompanyWalletOnboardingFundingStatus
{
    public bool IsSatisfied { get; init; }
    public decimal MinimumRequiredSyp { get; init; }
    public decimal CurrentBalanceSyp { get; init; }
    public bool HasSufficientPendingRequest { get; init; }

    public bool RequiresFundingGate => MinimumRequiredSyp > 0m && !IsSatisfied;
}

public interface ICompanyWalletOnboardingFundingService
{
    Task<decimal> GetRequiredMinimumSypAsync(CancellationToken cancellationToken = default);

    Task<CompanyWalletOnboardingFundingStatus> EvaluateAsync(
        int companyNetworkId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// جاهزية تمويل محفظة الشركة عند أول دخول: رصيد كافٍ أو طلب تغذية معلّق بمبلغ ≥ سعر إنشاء الشبكة.
/// </summary>
public sealed class CompanyWalletOnboardingFundingService(ApplicationDbContext db)
    : ICompanyWalletOnboardingFundingService
{
    public async Task<decimal> GetRequiredMinimumSypAsync(CancellationToken cancellationToken = default)
    {
        FeaturePricing? pricing = await db.FeaturePricings.AsNoTracking()
            .FirstOrDefaultAsync(
                p => p.IsActive
                     && p.FeatureKey == FeatureKeys.Networks
                     && p.BillingPeriod == PricingBillingPeriod.OneTime
                     && p.ChargeUnit == PricingChargeUnit.PerNetwork,
                cancellationToken);

        if (pricing == null)
        {
            return 0m;
        }

        decimal amount = WalletMath.CeilSyp(pricing.AmountSYP);
        return amount > 0m ? amount : 0m;
    }

    public async Task<CompanyWalletOnboardingFundingStatus> EvaluateAsync(
        int companyNetworkId,
        CancellationToken cancellationToken = default)
    {
        decimal minimum = await GetRequiredMinimumSypAsync(cancellationToken);
        if (minimum <= 0m)
        {
            return new CompanyWalletOnboardingFundingStatus
            {
                IsSatisfied = true,
                MinimumRequiredSyp = 0m,
                CurrentBalanceSyp = 0m,
                HasSufficientPendingRequest = false
            };
        }

        Network? network = await db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId, cancellationToken);
        if (network == null)
        {
            return new CompanyWalletOnboardingFundingStatus
            {
                IsSatisfied = false,
                MinimumRequiredSyp = minimum,
                CurrentBalanceSyp = 0m,
                HasSufficientPendingRequest = false
            };
        }

        int rootCompanyId = network.ParentNetworkId ?? network.Id;
        Network? company = network.ParentNetworkId == null
            ? network
            : await db.Networks.AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == rootCompanyId && n.ParentNetworkId == null, cancellationToken);

        decimal balance = company?.Balance ?? 0m;
        if (balance >= minimum)
        {
            return new CompanyWalletOnboardingFundingStatus
            {
                IsSatisfied = true,
                MinimumRequiredSyp = minimum,
                CurrentBalanceSyp = balance,
                HasSufficientPendingRequest = false
            };
        }

        bool hasPending = await db.NetworkTopUpRequests.AsNoTracking()
            .AnyAsync(
                r => r.NetworkId == rootCompanyId
                     && r.Status == NetworkTopUpRequestStatus.Pending
                     && r.Amount >= minimum,
                cancellationToken);

        return new CompanyWalletOnboardingFundingStatus
        {
            IsSatisfied = hasPending,
            MinimumRequiredSyp = minimum,
            CurrentBalanceSyp = balance,
            HasSufficientPendingRequest = hasPending
        };
    }
}
