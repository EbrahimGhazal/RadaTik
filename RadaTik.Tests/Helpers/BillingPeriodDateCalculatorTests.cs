using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class BillingPeriodDateCalculatorTests
{
    [Fact]
    public void AddPeriod_Monthly_AddsOneMonth()
    {
        DateTime baseDate = new(2026, 1, 15);
        DateTime result = BillingPeriodDateCalculator.AddPeriod(baseDate, PricingBillingPeriod.Monthly);
        Assert.Equal(new DateTime(2026, 2, 15), result);
    }

    [Fact]
    public void AddPeriod_Every12Months_AddsOneYear()
    {
        DateTime baseDate = new(2025, 5, 1);
        DateTime result = BillingPeriodDateCalculator.AddPeriod(baseDate, PricingBillingPeriod.Every12Months);
        Assert.Equal(new DateTime(2026, 5, 1), result);
    }
}
