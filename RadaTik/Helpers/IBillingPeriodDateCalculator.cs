using RadaTik.Models;

namespace RadaTik.Helpers;

public interface IBillingPeriodDateCalculator
{
    DateTime AddPeriod(DateTime baseDate, PricingBillingPeriod billingPeriod);
}

public sealed class BillingPeriodDateCalculatorAdapter : IBillingPeriodDateCalculator
{
    public DateTime AddPeriod(DateTime baseDate, PricingBillingPeriod billingPeriod) =>
        BillingPeriodDateCalculator.AddPeriod(baseDate, billingPeriod);
}
