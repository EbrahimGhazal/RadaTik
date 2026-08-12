using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.PricingPolicies;
using Xunit;

namespace RadaTik.Tests.Services;

public class PricingPolicyEngineTests
{
    [Fact]
    public void FixedAmountStrategy_ComputesRoundedSypAmount()
    {
        var strategy = new FixedAmountPricingScenarioStrategy();
        var pricing = new FeaturePricing
        {
            ChargeUnit = PricingChargeUnit.PerSector,
            AmountSYP = 1250.01m
        };

        var result = strategy.Compute(pricing, 0m);

        Assert.Equal(PricingChargeKind.FixedAmount, result.ChargeKind);
        Assert.Equal(1251m, result.EffectiveAmountSyp);
    }

    [Fact]
    public void PercentageStrategy_ComputesEstimatedAmountFromBase()
    {
        var strategy = new PercentagePricingScenarioStrategy();
        var pricing = new FeaturePricing
        {
            ChargeUnit = PricingChargeUnit.PercentOfCollectedAmount,
            AmountSYP = 2.5m
        };

        var result = strategy.Compute(pricing, 100000m);

        Assert.Equal(PricingChargeKind.Percentage, result.ChargeKind);
        Assert.Equal(2.5m, result.PercentValue);
        Assert.Equal(2500m, result.EffectiveAmountSyp);
    }

    [Fact]
    public void Compose_ForSenders_IncludesApprovalAndOldSypHint()
    {
        var composer = CreateComposer();
        var pricings = new List<FeaturePricing>
        {
            new()
            {
                FeatureKey = FeatureKeys.Sectors,
                BillingPeriod = PricingBillingPeriod.OneTime,
                ChargeUnit = PricingChargeUnit.PerSector,
                AmountSYP = 1000m,
                IsActive = true
            },
            new()
            {
                FeatureKey = FeatureKeys.Sectors,
                BillingPeriod = PricingBillingPeriod.Monthly,
                ChargeUnit = PricingChargeUnit.PerSector,
                AmountSYP = 150m,
                IsActive = true
            }
        };

        var draft = composer.Compose(FeatureKeys.Sectors, "المرسلات", pricings);

        Assert.Contains("إضافة الموظف المفوض", draft.PricingPolicyHtml);
        Assert.Contains("مدير النظام", draft.PricingPolicyHtml);
        Assert.Contains("× 100", draft.PricingPolicyHtml);
        Assert.Contains("قيمة التجديد × عدد الوحدات الفعلية", draft.PricingPolicyHtml);
    }

    [Fact]
    public void ResolveTemplate_WithAutoMarker_ReturnsGenerated()
    {
        var composer = CreateComposer();
        var generated = "<p>Generated</p>";

        var resolved = composer.ResolveTemplate(FeaturePublicContentTemplateMarkers.Auto, generated, FeaturePublicContentField.Detail);

        Assert.Equal(generated, resolved);
    }

    [Fact]
    public void BuildSimulation_ForSenders_RequiresEmployeeApproval()
    {
        var composer = CreateComposer();
        var pricings = new List<FeaturePricing>
        {
            new()
            {
                FeatureKey = FeatureKeys.Sectors,
                BillingPeriod = PricingBillingPeriod.OneTime,
                ChargeUnit = PricingChargeUnit.PerSector,
                AmountSYP = 1000m,
                IsActive = true
            }
        };

        var simulation = composer.BuildSimulationDescriptor(FeatureKeys.Sectors, "المرسلات", pricings);

        Assert.True(simulation.EmployeeActionRequiresApproval);
        Assert.Equal(PricingChargeKind.FixedAmount, simulation.ChargeKind);
        Assert.Equal(1000m, simulation.FixedAmountSyp);
        Assert.Equal(0, simulation.RenewalTimesPerYear);
    }

    [Fact]
    public void BuildSimulation_ForMonthlyRenewal_ExposesRenewalsPerYear()
    {
        var composer = CreateComposer();
        var pricings = new List<FeaturePricing>
        {
            new()
            {
                FeatureKey = FeatureKeys.Sectors,
                BillingPeriod = PricingBillingPeriod.OneTime,
                ChargeUnit = PricingChargeUnit.PerSector,
                AmountSYP = 10m,
                IsActive = true
            },
            new()
            {
                FeatureKey = FeatureKeys.Sectors,
                BillingPeriod = PricingBillingPeriod.Monthly,
                ChargeUnit = PricingChargeUnit.PerSector,
                AmountSYP = 2m,
                IsActive = true
            }
        };

        var simulation = composer.BuildSimulationDescriptor(FeatureKeys.Sectors, "المرسلات", pricings);

        Assert.Equal(12, simulation.RenewalTimesPerYear);
        Assert.Equal("2.00 ل.س.ج", simulation.RenewalChargeLabel);
    }

    private static FeaturePublicContentComposer CreateComposer()
    {
        return new FeaturePublicContentComposer(
            new ServicePricingPolicyCatalog(),
            new IPricingScenarioStrategy[]
            {
                new FixedAmountPricingScenarioStrategy(),
                new PercentagePricingScenarioStrategy()
            });
    }
}
