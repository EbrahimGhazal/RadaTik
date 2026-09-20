namespace RadaTik.Routing;

public static partial class RouteMappingExtensions
{
    private static void MapEmployeeRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "employee-dashboard",
            pattern: "employee/dashboard",
            defaults: new { area = "CompanyEmployee", controller = "Dashboard", action = "Index" });

        app.MapControllerRoute(
            name: "employee-wallet",
            pattern: "employee/wallet",
            defaults: new { area = "CompanyEmployee", controller = "Dashboard", action = "Wallet" });

        app.MapControllerRoute(
            name: "employee-my-tasks",
            pattern: "employee/my-tasks",
            defaults: new { area = "CompanyEmployee", controller = "MyEmployeeTasks", action = "Index" });

        app.MapControllerRoute(
            name: "employee-notifications",
            pattern: "employee/notifications/{action=Index}/{id?}",
            defaults: new { area = "CompanyEmployee", controller = "Notifications", action = "Index" });

        app.MapControllerRoute(
            name: "employee-my-payroll",
            pattern: "employee/my-payroll",
            defaults: new { area = "CompanyEmployee", controller = "MyPayroll", action = "Index" });

        app.MapControllerRoute(
            name: "employee-profile",
            pattern: "employee/profile",
            defaults: new { area = "CompanyEmployee", controller = "Account", action = "Profile" });

        app.MapControllerRoute(
            name: "employee-account",
            pattern: "employee/Account/{action=Profile}/{id?}",
            defaults: new { area = "CompanyEmployee", controller = "Account", action = "Profile" });

        app.MapControllerRoute(
            name: "employee-requestsManagement",
            pattern: "employee/RequestsManagement/{action=Index}/{id?}",
            defaults: new { area = "CompanyEmployee", controller = "RequestsManagement", action = "Index" });

        app.MapAreaControllerRoute(
            name: "companyEmployee-area",
            areaName: "CompanyEmployee",
            pattern: "employee/{controller=Dashboard}/{action=Index}/{id?}");
    }

    private static void MapCollectionPointRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "collectionPoint-dashboard",
            pattern: "collectionPoint/dashboard",
            defaults: new { area = "CollectionPoint", controller = "Dashboard", action = "Index" });

        app.MapControllerRoute(
            name: "collectionPoint-search-clients",
            pattern: "collectionPoint/SearchClients",
            defaults: new { area = "CollectionPoint", controller = "CollectionPoint", action = "SearchClients" });

        app.MapControllerRoute(
            name: "collectionPoint-dashboard-search-clients",
            pattern: "collectionPoint/Dashboard/SearchClients",
            defaults: new { area = "CollectionPoint", controller = "CollectionPoint", action = "SearchClients" });

        app.MapControllerRoute(
            name: "collectionPoint-wallet-client-topups",
            pattern: "collectionPoint/wallet/client-topups",
            defaults: new { area = "CollectionPoint", controller = "Wallet", action = "ClientTopUpRequests" });

        app.MapControllerRoute(
            name: "collectionPoint-wallet-topup",
            pattern: "collectionPoint/wallet/topup",
            defaults: new { area = "CollectionPoint", controller = "Wallet", action = "TopUp" });

        app.MapControllerRoute(
            name: "collectionPoint-receipts",
            pattern: "collectionPoint/receipts/{action=Index}/{id?}",
            defaults: new { area = "CollectionPoint", controller = "Receipts", action = "Index" });

        app.MapControllerRoute(
            name: "collectionPoint-actions",
            pattern: "collectionPoint/{action=Index}/{id?}",
            defaults: new { area = "CollectionPoint", controller = "CollectionPoint", action = "Index" });

        app.MapAreaControllerRoute(
            name: "collectionPoint-area",
            areaName: "CollectionPoint",
            pattern: "collectionPoint/{controller=Dashboard}/{action=Index}/{id?}");
    }

    private static void MapClientPortalRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "clientPortal-setup-requiredPassword",
            pattern: "clientPortal/setup/requiredPassword",
            defaults: new { area = "ClientPortal", controller = "Setup", action = "RequiredPassword" });

        app.MapControllerRoute(
            name: "clientPortal-dashboard",
            pattern: "clientPortal/dashboard",
            defaults: new { area = "ClientPortal", controller = "Dashboard", action = "Index" });

        app.MapControllerRoute(
            name: "clientPortal-mikrotik-traffic",
            pattern: "clientPortal/MikroTikTraffic/{action=Index}/{id?}",
            defaults: new { area = "ClientPortal", controller = "MikroTikTraffic", action = "Index" });

        app.MapControllerRoute(
            name: "clientPortal-actions",
            pattern: "clientPortal/{action=Index}/{id?}",
            defaults: new { area = "ClientPortal", controller = "ClientPortal", action = "Index" });

        app.MapAreaControllerRoute(
            name: "clientPortal-area",
            areaName: "ClientPortal",
            pattern: "clientPortal/{controller=Dashboard}/{action=Index}/{id?}");
    }
}
