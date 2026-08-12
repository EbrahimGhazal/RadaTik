namespace RadaTik.Routing;

public static partial class RouteMappingExtensions
{
    public static void MapRadaTikRoutes(this WebApplication app)
    {
        app.MapAccountRoutes();
        app.MapCompanyAdminRoutes();
        app.MapSystemAdminRoutes();
        app.MapEmployeeRoutes();
        app.MapCollectionPointRoutes();
        app.MapClientPortalRoutes();
        app.MapPublicSiteRoutes();
        app.MapFallbackAndApiRoutes();
    }
}
