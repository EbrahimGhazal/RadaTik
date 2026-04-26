using RadTik.Models;

namespace RadTik.Helpers;

public static class BillingPeriodDateCalculator
{
    public static DateTime AddPeriod(DateTime baseDate, PricingBillingPeriod billingPeriod)
    {
        return billingPeriod switch
        {
            PricingBillingPeriod.Daily => baseDate.AddDays(1),
            PricingBillingPeriod.Monthly => baseDate.AddMonths(1),
            PricingBillingPeriod.Every3Months => baseDate.AddMonths(3),
            PricingBillingPeriod.Every6Months => baseDate.AddMonths(6),
            PricingBillingPeriod.Every12Months => baseDate.AddYears(1),
            _ => baseDate.AddYears(10)
        };
    }
}
