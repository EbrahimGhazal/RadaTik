namespace RadaTik.Helpers;

public enum CompanyReportPeriodPreset
{
    Custom = 0,
    Today = 1,
    Week = 2,
    Month = 3,
    ThreeMonths = 4,
    SixMonths = 5,
    Year = 6
}

public readonly struct CompanyReportDateRange
{
    public CompanyReportDateRange(DateTime fromInclusive, DateTime toInclusive)
    {
        FromInclusive = fromInclusive;
        ToInclusive = toInclusive;
    }

    public DateTime FromInclusive { get; }
    public DateTime ToInclusive { get; }

    public static CompanyReportDateRange Resolve(
        CompanyReportPeriodPreset preset,
        DateTime? customFrom,
        DateTime? customTo)
    {
        var now = DateTime.Now;
        return preset switch
        {
            CompanyReportPeriodPreset.Today => new CompanyReportDateRange(now.Date, now),
            CompanyReportPeriodPreset.Week => new CompanyReportDateRange(now.Date.AddDays(-6), now),
            CompanyReportPeriodPreset.Month => new CompanyReportDateRange(new DateTime(now.Year, now.Month, 1), now),
            CompanyReportPeriodPreset.ThreeMonths => new CompanyReportDateRange(now.AddMonths(-3), now),
            CompanyReportPeriodPreset.SixMonths => new CompanyReportDateRange(now.AddMonths(-6), now),
            CompanyReportPeriodPreset.Year => new CompanyReportDateRange(new DateTime(now.Year, 1, 1), now),
            _ => ResolveCustom(customFrom, customTo, now)
        };
    }

    private static CompanyReportDateRange ResolveCustom(DateTime? customFrom, DateTime? customTo, DateTime now)
    {
        var from = customFrom ?? now.Date;
        var to = customTo ?? now;
        if (to < from)
        {
            (from, to) = (to, from);
        }

        return new CompanyReportDateRange(from, to);
    }
}
