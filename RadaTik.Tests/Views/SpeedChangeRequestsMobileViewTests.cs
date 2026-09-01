using Xunit;

namespace RadaTik.Tests.Views;

public sealed class SpeedChangeRequestsMobileViewTests
{
    [Fact]
    public void CompanyAdminSpeedChangeRequests_FitsViewportOnCompactScreens()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "RequestsManagement", "SpeedChangeRequests.cshtml"));
        Assert.Contains("radtk-page-speed-requests", view);
        Assert.Contains("radtk-data-table--cards", view);
        Assert.Contains("data-label=\"العميل\"", view);
        Assert.Contains("initSpeedChangeTableIfDesktop", view);
        Assert.Contains("speed-change-requests-cards.css", view);
        Assert.Contains("max-width: 1199.98px", view);
    }

    [Fact]
    public void SpeedChangeRequestsCardsCss_ClampsCardToViewport()
    {
        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "speed-change-requests-cards.css"));
        Assert.Contains("overflow-x: hidden", css);
        Assert.Contains("max-width: 100%", css);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", css);
        Assert.Contains("#speedChangeTable tbody tr", css);
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

        throw new FileNotFoundException("لم يتم العثور على ملف طلبات تغيير السرعة: " + Path.Combine(relativeParts));
    }
}
