using RadTik.Helpers;

namespace RadTik.ViewModels.CompanyAdmin;

public enum CompanyReportKind
{
    Subscribers = 1,
    Sectors = 2,
    Receivers = 3,
    Servers = 4,
    Subcontractors = 5
}

public sealed class CompanyReportsResultViewModel
{
    public required string Title { get; init; }
    public required string NetworkName { get; init; }
    public CompanyReportDateRange Range { get; init; }
    public required IReadOnlyList<string> Headers { get; init; }
    public required IReadOnlyList<IReadOnlyList<string>> Rows { get; init; }
    public decimal? ChargedAmountSyp { get; init; }

    /// <summary>عند true يُعرض المحتوى المخصص قبل/بعد الجدول بدل العنوان الافتراضي.</summary>
    public bool UseCustomTemplate { get; init; }

    public string? CustomHtmlBeforeTable { get; init; }
    public string? CustomHtmlAfterTable { get; init; }
}
