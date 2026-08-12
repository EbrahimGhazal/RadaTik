namespace RadaTik.Domain.CollectionPoint;

/// <summary>احتساب أشهر التأخر في الاشتراك (منطق نقي).</summary>
public static class SubscriptionArrearsCalculator
{
    public static int CalculatePendingMonths(DateTime? accountExpirationDate, DateTime now)
    {
        if (!accountExpirationDate.HasValue || accountExpirationDate.Value >= now)
        {
            return 1;
        }

        DateTime expiredDate = accountExpirationDate.Value.Date;
        DateTime today = now.Date;
        int months = (today.Year - expiredDate.Year) * 12 + today.Month - expiredDate.Month;
        if (today.Day > expiredDate.Day)
        {
            months++;
        }

        return Math.Max(1, months);
    }
}
