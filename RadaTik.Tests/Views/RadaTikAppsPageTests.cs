using Xunit;

namespace RadaTik.Tests.Views;

public sealed class RadaTikAppsPageTests
{
    [Fact]
    public void PublicController_ExposesDirectAndroidDownloadsForEachRole()
    {
        string controller = File.ReadAllText(FindFile("RadaTik", "Areas", "RadaTik", "Controllers", "PublicController.cs"));
        Assert.Contains("public async Task<IActionResult> Apps()", controller);
        Assert.Contains("DownloadAndroid()", controller);
        Assert.Contains("DownloadCollection()", controller);
        Assert.Contains("DownloadEmployee()", controller);
        Assert.Contains("radatik-client.apk", controller);
        Assert.Contains("radatik-collection.apk", controller);
        Assert.Contains("radatik-employee.apk", controller);
        Assert.Contains("application/vnd.android.package-archive", controller);
        Assert.Contains("ClientDownloads", controller);
        Assert.Contains("IncrementAsync", controller);
    }

    [Fact]
    public void AppsView_OffersSideloadDownloadWithoutPlayStore()
    {
        string view = File.ReadAllText(FindFile("RadaTik", "Areas", "RadaTik", "Views", "Public", "Apps.cshtml"));
        Assert.Contains("/RadaTik/DownloadAndroid", view);
        Assert.Contains("/RadaTik/DownloadCollection", view);
        Assert.Contains("/RadaTik/DownloadEmployee", view);
        Assert.Contains("RadaTik المشترك", view);
        Assert.Contains("RadaTik التحصيل", view);
        Assert.Contains("RadaTik الموظف", view);
        Assert.Contains("عدد التحميلات", view);
        Assert.Contains("DownloadCount(\"Client\")", view);
        Assert.Contains("DownloadCount(\"Collection\")", view);
        Assert.Contains("DownloadCount(\"Employee\")", view);
        Assert.DoesNotContain("play.google.com", view);
    }

    [Fact]
    public void PublicRoutes_MapRoleAppDownloads()
    {
        string routes = File.ReadAllText(FindFile("RadaTik", "Routing", "RouteMappingExtensions.PublicSites.cs"));
        Assert.Contains("RadaTik/DownloadAndroid", routes);
        Assert.Contains("RadaTik/DownloadCollection", routes);
        Assert.Contains("RadaTik/DownloadEmployee", routes);
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

        throw new FileNotFoundException("لم يتم العثور على ملف صفحة التطبيقات: " + Path.Combine(relativeParts));
    }
}
