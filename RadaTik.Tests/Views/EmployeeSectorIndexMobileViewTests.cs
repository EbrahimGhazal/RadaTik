using Xunit;

namespace RadaTik.Tests.Views;

public sealed class EmployeeSectorIndexMobileViewTests
{
    [Fact]
    public void CompanyEmployeeSectorIndex_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyEmployee", "Views", "Sector", "Index.cshtml"));
        Assert.Contains("radtk-page-emp-sector-index", view);
        Assert.Contains("employee-sector-page", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("employee-sector-cards.css", view);
        Assert.Contains("data-label=\"القطاع\"", view);
        Assert.Contains("data-label=\"IP\"", view);
        Assert.Contains("radtk-col-actions", view);
        Assert.Contains("employee-sector-actions__label", view);
        Assert.Contains("id=\"employeeSectorsTable\"", view);
    }

    [Fact]
    public void EmployeeSectorCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "employee-sector-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("#employeeSectorsTable tbody tr", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("visibility: visible !important", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف قطاعات الموظف: " + Path.Combine(relativeParts));
    }
}
