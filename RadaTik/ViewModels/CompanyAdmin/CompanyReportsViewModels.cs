using RadaTik.Helpers;

namespace RadaTik.ViewModels.CompanyAdmin;

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

    /// <summary>يُملأ عند إعادة إرسال النموذج لخصم الطباعة؛ يُستخدم لتلميح رسالة التأكيد.</summary>
    public CompanyReportKind Kind { get; init; }

    public CompanyReportPeriodPreset Period { get; init; }
    public DateTime? CustomFrom { get; init; }
    public DateTime? CustomTo { get; init; }

    /// <summary>سعر التصدير/الطباعة المقرب (ReportsExport) لعرضه في التأكيد؛ null إن لم يُعرّف تسعير.</summary>
    public decimal? ExportPriceHintSyp { get; init; }

    /// <summary>يُعرض فقط عندما يُنفّذ خصم مع طلب «عرض» (مثلاً إن أُضيف لاحقاً)؛ الطباعة من هذه الصفحة تُخصم عبر ChargeForPrint.</summary>
    public decimal? ChargedAmountSyp { get; init; }

    /// <summary>جدول HTML للبيانات (صفوف × أعمدة)، آمن للعرض عبر Html.Raw.</summary>
    public required string DataTableHtml { get; init; }

    /// <summary>عند وجود قالب يحتوي {{DATA_TABLE}}: HTML كامل بعد دمج الجدول داخل القالب (عرض واحد).</summary>
    public string? IntegratedBodyHtml { get; init; }

    /// <summary>عند true يُعرض المحتوى المخصص قبل/بعد الجدول بدل العنوان الافتراضي.</summary>
    public bool UseCustomTemplate { get; init; }

    public string? CustomHtmlBeforeTable { get; init; }
    public string? CustomHtmlAfterTable { get; init; }
}
