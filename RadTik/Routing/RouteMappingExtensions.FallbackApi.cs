using RadTik.Hubs;

namespace RadTik.Routing;

public static partial class RouteMappingExtensions
{
    private static void MapFallbackAndApiRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "areas",
            pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

        app.MapControllerRoute(
            name: "default",
            pattern: "{controller=Public}/{action=Index}/{id?}");

        app.MapControllers();
        app.MapHub<TrafficHub>("/hubs/traffic");
    }
}
