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

        // الصفحة الجذرية → SkyBeam (الموقع الحالي للعملاء)
        app.MapGet("/", () => Results.Redirect("/skyBeam", permanent: false));
    }
}
