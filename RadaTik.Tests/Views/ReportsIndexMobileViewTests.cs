using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ReportsIndexMobileViewTests
{
    [Fact]
    public void CompanyAdminReportsIndex_UsesStackedSettingsOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Reports", "Index.cshtml"));
        Assert.Contains("radtk-page-reports-index", view);
        Assert.Contains("financial-reports-page", view);
        Assert.Contains("reports-index-cards.css", view);
        Assert.Contains("report-settings-grid", view);
        Assert.Contains("report-settings-actions", view);
        Assert.Contains("id=\"reportKindSelect\"", view);
        Assert.DoesNotContain("report-settings-table", view);
    }

    [Fact]
    public void ReportsIndexCardsCss_ClampsPageToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "reports-index-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("report-settings-grid", css);
        Assert.Contains("grid-template-columns: 1fr", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف التقارير: " + Path.Combine(relativeParts));
    }
}
