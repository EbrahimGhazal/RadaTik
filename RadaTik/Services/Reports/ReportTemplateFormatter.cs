using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.ViewModels.CompanyAdmin;

namespace RadaTik.Services.Reports;

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
        string reportTitle = GetReportTitleDisplay(kind);
        string companyName = companyNetwork.Name ?? "";
        string networkName = selectedNetwork.Name ?? "";

        Dictionary<string, string> dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
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
            string key = m.Groups[1].Value.Trim();
            if (key.Equals("DATA_TABLE", StringComparison.OrdinalIgnoreCase))
            {
                return DataTableMarker;
            }

            return vars.TryGetValue(key, out string? v) ? v : m.Value;
        });
    }

    public static (string BeforeHtml, string AfterHtml) SplitAtDataTable(string merged)
    {
        if (string.IsNullOrEmpty(merged))
        {
            return ("", "");
        }

        int idx = merged.IndexOf(DataTableMarker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return (merged, "");
        }

        string before = merged[..idx];
        string after = merged[(idx + DataTableMarker.Length)..];
        return (before, after);
    }

    /// <summary>
    /// يُنشئ جدول HTML حقيقي (thead/tbody، صفوف وأعمدة) من رؤوس وأعمدة التقرير، مع ترميز آمن للنصوص.
    /// يُستخدم لإدراج البيانات في القالب عند <see cref="DataTableMarker"/> وللعرض الافتراضي.
    /// </summary>
    /// <param name="additionalTableClass">فئات CSS إضافية للجدول (مثلاً تمييز تقرير المرسلات).</param>
    /// <param name="caption">عنوان يظهر فوق الجدول (اسم التقرير).</param>
    public static string BuildDataTableHtml(
        IReadOnlyList<string> headers,
        IReadOnlyList<IReadOnlyList<string>> rows,
        string? additionalTableClass = null,
        string? caption = null)
    {
        int colCount = Math.Max(1, headers.Count);
        StringBuilder sb = new StringBuilder(512 + rows.Count * headers.Count * 32);
        sb.Append("<div class=\"table-responsive report-table-shell\">");
        string tableClasses = "table table-sm table-bordered report-data-table report-data-table-elegant no-responsive-stack mb-0";
        if (!string.IsNullOrWhiteSpace(additionalTableClass))
        {
            tableClasses += " " + additionalTableClass.Trim();
        }

        sb.Append("<table class=\"").Append(tableClasses).Append("\" dir=\"rtl\" role=\"table\">");
        if (!string.IsNullOrWhiteSpace(caption))
        {
            sb.Append("<caption class=\"report-data-table-caption\">")
                .Append(WebUtility.HtmlEncode(caption.Trim()))
                .Append("</caption>");
        }

        sb.Append("<thead><tr>");
        if (headers.Count > 0)
        {
            foreach (string h in headers)
            {
                sb.Append("<th scope=\"col\">").Append(WebUtility.HtmlEncode(h)).Append("</th>");
            }
        }
        else
        {
            sb.Append("<th scope=\"col\">").Append(WebUtility.HtmlEncode("—")).Append("</th>");
        }

        sb.Append("</tr></thead><tbody>");
        if (rows.Count == 0)
        {
            sb.Append("<tr><td colspan=\"")
                .Append(colCount)
                .Append("\" class=\"text-center text-muted py-4\">")
                .Append(WebUtility.HtmlEncode("لا توجد بيانات ضمن هذه الفترة."))
                .Append("</td></tr>");
        }
        else
        {
            foreach (IReadOnlyList<string> row in rows)
            {
                sb.Append("<tr>");
                for (int i = 0; i < headers.Count; i++)
                {
                    string cell = i < row.Count ? row[i] ?? "" : "";
                    string encoded = WebUtility.HtmlEncode(cell);
                    if (i == 0)
                    {
                        sb.Append("<th scope=\"row\" class=\"report-data-row-head\">").Append(encoded).Append("</th>");
                    }
                    else
                    {
                        sb.Append("<td>").Append(encoded).Append("</td>");
                    }
                }

                sb.Append("</tr>");
            }
        }

        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    public static string StripHtmlForPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return "";
        }

        string noTags = Regex.Replace(html, "<[^>]+>", "\n");
        string decoded = System.Net.WebUtility.HtmlDecode(noTags);
        return Regex.Replace(decoded, @"[ \t]+\r?\n", "\n").Trim();
    }
}
