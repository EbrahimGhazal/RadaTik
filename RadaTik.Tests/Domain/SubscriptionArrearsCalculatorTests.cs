using RadaTik.Domain.CollectionPoint;
using Xunit;

namespace RadaTik.Tests.Domain;

public sealed class SubscriptionArrearsCalculatorTests
{
    [Fact]
    public void CalculatePendingMonths_WhenNotExpired_ReturnsOne()
    {
        DateTime now = new(2026, 5, 15);
        DateTime expiration = new(2026, 6, 1);

        int months = SubscriptionArrearsCalculator.CalculatePendingMonths(expiration, now);

        Assert.Equal(1, months);
    }

    [Fact]
    public void CalculatePendingMonths_WhenExpired_ReturnsAtLeastOne()
    {
        DateTime now = new(2026, 5, 15);
        DateTime expiration = new(2026, 3, 10);

        int months = SubscriptionArrearsCalculator.CalculatePendingMonths(expiration, now);

        Assert.True(months >= 2);
    }
}
