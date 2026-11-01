using Xunit;

namespace RadaTik.Tests.Views;

public sealed class AiTechnicalReportViewTests
{
    [Fact]
    public void ReportView_ClonesStatsJsonElementsBeforeDocumentDispose()
    {
        string view = File.ReadAllText(FindFile(
            "RadaTik", "Views", "CustomerService", "AiTechnicalReport.cshtml"));
        Assert.Contains("JsonDocument.Parse(statsJson)", view);
        Assert.Contains("e.Clone()", view);
        Assert.Contains("EnumerateArray().Select(e => e.Clone()).ToList()", view);
    }

    [Fact]
    public void ReportView_AllowsShowingAndHidingChartSeries()
    {
        string view = File.ReadAllText(FindFile(
            "RadaTik", "Views", "CustomerService", "AiTechnicalReport.cshtml"));
        Assert.Contains("techReportChartControls", view);
        Assert.Contains("data-chart-preset=\"rx\"", view);
        Assert.Contains("data-chart-preset=\"tx\"", view);
        Assert.Contains("setDatasetVisibility", view);
        Assert.Contains("techReportSeriesToggles", view);

        string css = File.ReadAllText(FindFile(
            "RadaTik", "wwwroot", "css", "customer-service-assistant.css"));
        Assert.Contains(".cs-tech-chart-controls", css);
        Assert.Contains(".cs-tech-chart-chip.is-on", css);
    }

    private static string FindFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
