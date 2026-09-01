using Xunit;

namespace RadaTik.Tests.Views;

public sealed class CashBoxMobileViewTests
{
    [Fact]
    public void CompanyAdminCashBoxIndex_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "CashBox", "Index.cshtml"));
        Assert.Contains("financial-cashbox-page", view);
        Assert.Contains("cashbox-cards.css", view);
        Assert.Contains("data-label=\"التاريخ\"", view);
        Assert.Contains("data-label=\"المبلغ\"", view);
        Assert.Contains("cashbox-log-table", view);
        Assert.Contains("radtk-data-table--cards", view);
    }

    [Fact]
    public void CashBoxCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "cashbox-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("cashbox-log-table tbody tr", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف الصندوق النقدي: " + Path.Combine(relativeParts));
    }
}
