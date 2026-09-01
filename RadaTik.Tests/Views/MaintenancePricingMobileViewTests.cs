using Xunit;

namespace RadaTik.Tests.Views;

public sealed class MaintenancePricingMobileViewTests
{
    [Fact]
    public void CompanyAdminMaintenancePricing_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "MaintenancePricing", "Index.cshtml"));
        Assert.Contains("radtk-page-maintenance-pricing", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("maintenance-pricing-cards.css", view);
        Assert.Contains("data-label=\"طريقة الحل\"", view);
        Assert.Contains("id=\"maintenancePricingTable\"", view);
    }

    [Fact]
    public void MaintenancePricingCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "maintenance-pricing-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("#maintenancePricingTable tbody tr.maintenance-pricing-row", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
    }

    [Fact]
    public void SharedCardTables_ConvertOnTabletNotOnlyPhone()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "radtk-ui-kit.css"));
        Assert.Contains("@media (max-width: 1199.98px)", css);
        Assert.Contains("table.radtk-data-table.radtk-data-table--cards", css);
        Assert.Contains("min-width: 0 !important", css);
        Assert.DoesNotContain("max-width: 58%;", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف تسعير الصيانة: " + Path.Combine(relativeParts));
    }
}
