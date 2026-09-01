using Xunit;

namespace RadaTik.Tests.Views;

public sealed class MikroTikServersMobileViewTests
{
    [Fact]
    public void CompanyAdminMikroTikServersIndex_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "MikroTikServers", "Index.cshtml"));
        Assert.Contains("radtk-page-mikrotik-servers", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("mikrotik-servers-cards.css", view);
        Assert.Contains("data-label=\"اسم الخادم\"", view);
        Assert.Contains("radtk-col-actions", view);
        Assert.Contains("mikrotik-servers-actions__label", view);
    }

    [Fact]
    public void MikroTikServersCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "mikrotik-servers-cards.css"));
        Assert.Contains("max-width: 1199.98px", css);
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("#mikrotikServersTable tbody tr", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف خوادم مايكروتك: " + Path.Combine(relativeParts));
    }
}
