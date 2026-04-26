using System.Text.RegularExpressions;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.ViewModels.CompanyAdmin;

namespace RadTik.Services.Reports;

public static class ReportTemplateFormatter
{
    public const string DataTableMarker = "{{DATA_TABLE}}";

    private static readonly Regex PlaceholderRegex = new(@"\{\{\s*([^}]+?)\s*\}\}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, string> BuildStandardPlaceholders(
        Network companyNetwork,
        Network selectedNetwork,
        CompanyReportKind kind,
        CompanyReportDateRange range,
        int rowCount,
        ApplicationUser? manager,
        DateTime generatedAtLocal)
    {
        var reportTitle = GetReportTitleDisplay(kind);
        var companyName = companyNetwork.Name ?? "";
        var networkName = selectedNetwork.Name ?? "";

        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["CompanyName"] = companyName,
            ["NetworkName"] = networkName,
            ["ReportTitle"] = reportTitle,
            ["PeriodFrom"] = range.FromInclusive.ToString("yyyy/MM/dd HH:mm"),
            ["PeriodTo"] = range.ToInclusive.ToString("yyyy/MM/dd HH:mm"),
            ["PeriodFromDate"] = range.FromInclusive.ToString("yyyy/MM/dd"),
            ["PeriodToDate"] = range.ToInclusive.ToString("yyyy/MM/dd"),
            ["RowCount"] = rowCount.ToString(),
            ["GeneratedAt"] = generatedAtLocal.ToString("yyyy/MM/dd HH:mm"),
            ["ManagerName"] = string.IsNullOrWhiteSpace(manager?.FullName) ? (manager?.UserName ?? "") : manager!.FullName!,
            ["ManagerEmail"] = manager?.Email ?? "",
            ["ManagerUserName"] = manager?.UserName ?? ""
        };

        return dict;
    }

    public static string GetReportTitleDisplay(CompanyReportKind kind) => kind switch
    {
        CompanyReportKind.Subscribers => "تقرير المشتركين",
        CompanyReportKind.Sectors => "تقرير المرسلات (القطاعات)",
        CompanyReportKind.Receivers => "تقرير المستقبلات",
        CompanyReportKind.Servers => "تقرير خوادم MikroTik",
        CompanyReportKind.Subcontractors => "تقرير المتعاقدين بالباطن (نقاط التحصيل)",
        _ => "تقرير"
    };

    /// <summary>يستبدل {{Key}} بالقيم المعروفة فقط؛ أي placeholder غير معروف يُترك كما هو.</summary>
    public static string ReplacePlaceholders(string? template, IReadOnlyDictionary<string, string> vars)
    {
        if (string.IsNullOrWhiteSpace(template))
        {
            return "";
        }

        return PlaceholderRegex.Replace(template, m =>
        {
            var key = m.Groups[1].Value.Trim();
            if (key.Equals("DATA_TABLE", StringComparison.OrdinalIgnoreCase))
            {
                return DataTableMarker;
            }

            return vars.TryGetValue(key, out var v) ? v : m.Value;
        });
    }

    public static (string BeforeHtml, string AfterHtml) SplitAtDataTable(string merged)
    {
        if (string.IsNullOrEmpty(merged))
        {
            return ("", "");
        }

        var idx = merged.IndexOf(DataTableMarker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return (merged, "");
        }

        var before = merged[..idx];
        var after = merged[(idx + DataTableMarker.Length)..];
        return (before, after);
    }

    public static string StripHtmlForPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        var noTags = Regex.Replace(html, "<[^>]+>", "\n");
        var decoded = System.Net.WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"[ \t]+\r?\n", "\n").Trim();
    }
}
