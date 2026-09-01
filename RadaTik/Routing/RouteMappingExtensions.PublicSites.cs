namespace RadaTik.Routing;

public static partial class RouteMappingExtensions
{
    private static void MapPublicSiteRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "skyBeam-home",
            pattern: "skyBeam",
            defaults: new { area = "SkyBeam", controller = "Public", action = "Index" });

        app.MapAreaControllerRoute(
            name: "skyBeam-area",
            areaName: "SkyBeam",
            pattern: "skyBeam/{action=Index}/{id?}",
            defaults: new { controller = "Public" });

        app.MapControllerRoute(
            name: "radatik-home",
            pattern: "RadaTik",
            defaults: new { area = "RadaTik", controller = "Public", action = "Index" });

        app.MapControllerRoute(
            name: "radatik-apps-android",
            pattern: "RadaTik/Apps/android",
            defaults: new { area = "RadaTik", controller = "Public", action = "DownloadAndroid" });

        app.MapControllerRoute(
            name: "radatik-download-android",
            pattern: "RadaTik/DownloadAndroid",
            defaults: new { area = "RadaTik", controller = "Public", action = "DownloadAndroid" });

        app.MapControllerRoute(
            name: "radatik-download-collection",
            pattern: "RadaTik/DownloadCollection",
            defaults: new { area = "RadaTik", controller = "Public", action = "DownloadCollection" });

        app.MapControllerRoute(
            name: "radatik-download-employee",
            pattern: "RadaTik/DownloadEmployee",
            defaults: new { area = "RadaTik", controller = "Public", action = "DownloadEmployee" });

        app.MapControllerRoute(
            name: "radatik-download-company",
            pattern: "RadaTik/DownloadCompany",
            defaults: new { area = "RadaTik", controller = "Public", action = "DownloadCompany" });

        app.MapAreaControllerRoute(
            name: "radatik-area",
            areaName: "RadaTik",
            pattern: "RadaTik/{action=Index}/{id?}",
            defaults: new { controller = "Public" });

        // توافق مع الروابط القديمة /Public/*
        app.MapControllerRoute(
            name: "legacy-public-root",
            pattern: "Public/{action=Index}/{id?}",
            defaults: new { area = "SkyBeam", controller = "Public" });

        app.MapControllerRoute(
            name: "robots-txt",
            pattern: "robots.txt",
            defaults: new { area = "RadaTik", controller = "Public", action = "Robots" });

        app.MapControllerRoute(
            name: "sitemap-xml",
            pattern: "sitemap.xml",
            defaults: new { area = "RadaTik", controller = "Public", action = "Sitemap" });

        // الصفحة الجذرية → الموقع العام بعنوانه القانوني
        app.MapGet("/", () => Results.Redirect("/RadaTik", permanent: true));
    }
}
