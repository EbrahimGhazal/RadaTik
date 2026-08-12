using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services.PricingPolicies;

public sealed class CollectionCommissionPricingComputation
{
    public bool IsSupported { get; init; }
    public decimal PercentValue { get; init; }
    public decimal FeeAmountSyp { get; init; }
}

public interface ICollectionCommissionPricingStrategy
{
    bool CanHandle(FeaturePricing pricing);
    CollectionCommissionPricingComputation Compute(FeaturePricing pricing, decimal paymentAmountSyp);
}

public sealed class PercentageCollectionCommissionPricingStrategy : ICollectionCommissionPricingStrategy
{
    public bool CanHandle(FeaturePricing pricing) =>
        pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount;

    public CollectionCommissionPricingComputation Compute(FeaturePricing pricing, decimal paymentAmountSyp)
    {
        var percent = Math.Clamp(pricing.AmountSYP, 0m, 100m);
        var rawFee = paymentAmountSyp <= 0m ? 0m : paymentAmountSyp * (percent / 100m);
        var fee = WalletMath.CeilSyp(rawFee);

        return new CollectionCommissionPricingComputation
        {
            IsSupported = true,
            PercentValue = percent,
            FeeAmountSyp = fee
        };
    }
}

public interface ICollectionCommissionPricingResolver
{
    CollectionCommissionPricingComputation Resolve(FeaturePricing pricing, decimal paymentAmountSyp);
}

public sealed class CollectionCommissionPricingResolver : ICollectionCommissionPricingResolver
{
    private readonly IReadOnlyList<ICollectionCommissionPricingStrategy> _strategies;

    public CollectionCommissionPricingResolver(IEnumerable<ICollectionCommissionPricingStrategy> strategies)
    {
        _strategies = strategies.ToList();
    }

    public CollectionCommissionPricingComputation Resolve(FeaturePricing pricing, decimal paymentAmountSyp)
    {
        var strategy = _strategies.FirstOrDefault(s => s.CanHandle(pricing));
        if (strategy == null)
        {
            return new CollectionCommissionPricingComputation
            {
                IsSupported = false
            };
        }

        return strategy.Compute(pricing, paymentAmountSyp);
    }
}
