namespace RadTik.Routing;

public static partial class RouteMappingExtensions
{
    public static void MapRadTikRoutes(this WebApplication app)
    {
        app.MapAccountRoutes();
        app.MapCompanyAdminRoutes();
        app.MapSystemAdminRoutes();
        app.MapEmployeeRoutes();
        app.MapCollectionPointRoutes();
        app.MapClientPortalRoutes();
        app.MapFallbackAndApiRoutes();
    }
}
