using Xunit;

namespace RadaTik.Tests.Views;

public sealed class ReceiverCreateMobileViewTests
{
    [Fact]
    public void CompanyAdminCreate_HasMobileMapLayout()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyAdmin", "Views", "Receiver", "Create.cshtml"));
        string map = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_ReceiverCreateMap.cshtml"));
        string scripts = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_ReceiverCreateMapScripts.cshtml"));
        Assert.Contains("receiver-create-page", text);
        Assert.Contains("<partial name=\"_ReceiverCreateMap\" />", text);
        Assert.Contains("<partial name=\"_ReceiverCreateMapScripts\" />", text);
        Assert.Contains("receiver-map-wrap", map);
        Assert.Contains("receiver-map-toolbar", map);
        Assert.Contains("btnUseMyLocation", map);
        Assert.Contains("invalidateSize", scripts);
        Assert.Contains("fitCoverageInView", scripts);
        Assert.Contains("--receiver-map-height", text);
        Assert.DoesNotContain("height: 250px", text);

        string css = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "radatik-responsive-spa.css"));
        Assert.Contains(".leaflet-overlay-pane svg", css);
        Assert.Contains("max-width: none !important", css);
        Assert.DoesNotContain(".leaflet-container .leaflet-pane svg", css);

        string mapCss = File.ReadAllText(FindFile("RadaTik", "wwwroot", "css", "receiver-create-map.css"));
        Assert.Contains(".leaflet-overlay-pane svg", mapCss);
    }

    [Fact]
    public void CompanyEmployeeCreate_IncludesSharedMap()
    {
        string text = File.ReadAllText(FindFile("RadaTik", "Areas", "CompanyEmployee", "Views", "Receiver", "Create.cshtml"));
        Assert.Contains("receiver-create-page", text);
        Assert.Contains("<partial name=\"_ReceiverCreateMap\" />", text);
        Assert.Contains("<partial name=\"_ReceiverCreateMapScripts\" />", text);
        Assert.Contains("id=\"sectorSelect\"", text);
        Assert.Contains("id=\"latitudeInput\"", text);
        Assert.Contains("id=\"map\"", File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_ReceiverCreateMap.cshtml")));
        Assert.Contains("L.map", File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_ReceiverCreateMapScripts.cshtml")));
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

        throw new FileNotFoundException("لم يتم العثور على الملف: " + Path.Combine(relativeParts));
    }
}
