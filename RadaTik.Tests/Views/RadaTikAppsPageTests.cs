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
        Assert.Contains("DownloadCompany()", controller);
        Assert.Contains("radatik-client.apk", controller);
        Assert.Contains("radatik-collection.apk", controller);
        Assert.Contains("radatik-employee.apk", controller);
        Assert.Contains("radatik-company.apk", controller);
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
        Assert.Contains("/RadaTik/DownloadCompany", view);
        Assert.Contains("RadaTik المشترك", view);
        Assert.Contains("RadaTik التحصيل", view);
        Assert.Contains("RadaTik الموظف", view);
        Assert.Contains("RadaTik مدير الشركة", view);
        Assert.Contains("عدد التحميلات", view);
        Assert.Contains("DownloadCount(\"Client\")", view);
        Assert.Contains("DownloadCount(\"Collection\")", view);
        Assert.Contains("DownloadCount(\"Employee\")", view);
        Assert.Contains("DownloadCount(\"Company\")", view);
        Assert.DoesNotContain("play.google.com", view);
    }

    [Fact]
    public void PublicSite_ExposesSearchEngineBasics()
    {
        string layout = File.ReadAllText(FindFile("RadaTik", "Areas", "RadaTik", "Views", "Shared", "_PublicLayout.cshtml"));
        Assert.Contains("rel=\"canonical\"", layout);
        Assert.Contains("og:title", layout);
        Assert.Contains("application/ld+json", layout);
        Assert.Contains("sitemap.xml", layout);

        string controller = File.ReadAllText(FindFile("RadaTik", "Areas", "RadaTik", "Controllers", "PublicController.cs"));
        Assert.Contains("public IActionResult Robots()", controller);
        Assert.Contains("public IActionResult Sitemap()", controller);

        string routes = File.ReadAllText(FindFile("RadaTik", "Routing", "RouteMappingExtensions.PublicSites.cs"));
        Assert.Contains("robots.txt", routes);
        Assert.Contains("sitemap.xml", routes);
        Assert.Contains("permanent: true", routes);

        string home = File.ReadAllText(FindFile("RadaTik", "Controllers", "HomeController.cs"));
        Assert.Contains("RedirectPermanent(\"/RadaTik\")", home);
        Assert.DoesNotContain("Redirect(\"/radatik\")", home);

        string shell = File.ReadAllText(FindFile("RadaTik", "Views", "Shared", "_Shell.cshtml"));
        Assert.Contains("noindex, nofollow", shell);

        string robots = RadaTik.Helpers.PublicSeo.RobotsTxt("https://radatik.com");
        Assert.Contains("Sitemap: https://radatik.com/sitemap.xml", robots);
        Assert.Contains("Disallow: /networkManager", robots);
        Assert.Contains("Allow: /RadaTik", robots);

        string sitemap = RadaTik.Helpers.PublicSeo.SitemapXml("https://radatik.com", new DateTimeOffset(2026, 8, 30, 0, 0, 0, TimeSpan.Zero));
        Assert.Contains("https://radatik.com/RadaTik/Apps", sitemap);
        Assert.Contains("<lastmod>2026-08-30</lastmod>", sitemap);
    }

    [Fact]
    public void PublicRoutes_MapRoleAppDownloads()
    {
        string routes = File.ReadAllText(FindFile("RadaTik", "Routing", "RouteMappingExtensions.PublicSites.cs"));
        Assert.Contains("RadaTik/DownloadAndroid", routes);
        Assert.Contains("RadaTik/DownloadCollection", routes);
        Assert.Contains("RadaTik/DownloadEmployee", routes);
        Assert.Contains("RadaTik/DownloadCompany", routes);
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
