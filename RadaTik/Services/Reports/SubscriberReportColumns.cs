using RadaTik.Models;
using RadaTik.ViewModels.CompanyAdmin;

namespace RadaTik.Services.Reports;

public static class SubscriberReportColumns
{
    public const string SequenceKey = ReportPrintColumns.SequenceKey;
    public const string SequenceTitle = ReportPrintColumns.SequenceTitle;

    public static IReadOnlyList<ReportColumnOption> Selectable =>
        ReportPrintColumns.Selectable(CompanyReportKind.Subscribers);

    public static IReadOnlyList<string> DefaultKeys =>
        ReportPrintColumns.DefaultKeys(CompanyReportKind.Subscribers);

    public static IReadOnlyList<string> ResolveSelected(IEnumerable<string>? requested) =>
        ReportPrintColumns.ResolveSelected(CompanyReportKind.Subscribers, requested);

    public static string Serialize(IEnumerable<string> keys) =>
        ReportPrintColumns.Serialize(CompanyReportKind.Subscribers, keys);

    public static IReadOnlyList<string> Deserialize(string? stored) =>
        ReportPrintColumns.Deserialize(CompanyReportKind.Subscribers, stored);

    public static (string[] Headers, List<IReadOnlyList<string>> Rows) Build(
        IReadOnlyList<Client> clients,
        IEnumerable<string>? requestedColumns) =>
        ReportPrintColumns.BuildSubscribers(clients, requestedColumns);
}
