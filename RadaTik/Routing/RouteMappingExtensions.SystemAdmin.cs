namespace RadaTik.Routing;

public static partial class RouteMappingExtensions
{
    private static void MapSystemAdminRoutes(this WebApplication app)
    {
        app.MapGet("/systemAdmin/JoinRequests/PendingCounts", (HttpContext context) =>
        {
            string query = context.Request.QueryString.HasValue ? context.Request.QueryString.Value : string.Empty;
            return Results.Redirect($"/systemAdmin/notifications/PendingCounts{query}");
        });

        app.MapControllerRoute(
            name: "systemAdmin-joinRequests",
            pattern: "systemAdmin/JoinRequests/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "JoinRequests", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-services",
            pattern: "systemAdmin/services/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "ServiceCatalog", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-serviceRequests",
            pattern: "systemAdmin/serviceRequests/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "ServiceRequests", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-topUpRequests",
            pattern: "systemAdmin/topUpRequests/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "TopUpRequests", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-collectionPointTopUpRequests",
            pattern: "systemAdmin/collectionPointTopUpRequests/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "CollectionPointTopUpRequests", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-passwordResetRequests",
            pattern: "systemAdmin/passwordResetRequests/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "PasswordResetRequests", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-fundingRequests",
            pattern: "systemAdmin/fundingRequests/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "FundingRequests", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-paymentMethods",
            pattern: "systemAdmin/paymentMethods/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "PaymentMethods", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-receipts",
            pattern: "systemAdmin/receipts/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "Receipts", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-serviceCatalog",
            pattern: "systemAdmin/serviceCatalog/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "ServiceCatalog", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-notifications",
            pattern: "systemAdmin/notifications/{action=PendingCounts}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "Notifications", action = "PendingCounts" });

        app.MapControllerRoute(
            name: "systemAdmin-account-profile",
            pattern: "systemAdmin/Account/profile",
            defaults: new { area = "SystemAdmin", controller = "Account", action = "Profile" });

        app.MapControllerRoute(
            name: "systemAdmin-account",
            pattern: "systemAdmin/Account/{action=Profile}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "Account", action = "Profile" });

        app.MapControllerRoute(
            name: "systemAdmin-cashbox",
            pattern: "systemAdmin/CashBox/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "CashBox", action = "Index" });

        app.MapControllerRoute(
            name: "systemAdmin-onboarding-dismiss",
            pattern: "systemAdmin/onboarding/dismiss",
            defaults: new { area = "SystemAdmin", controller = "Onboarding", action = "Dismiss" });

        app.MapControllerRoute(
            name: "systemAdmin-setup-requiredPassword",
            pattern: "systemAdmin/setup/requiredPassword",
            defaults: new { area = "SystemAdmin", controller = "Setup", action = "RequiredPassword" });

        app.MapControllerRoute(
            name: "systemAdmin-setup-pricing",
            pattern: "systemAdmin/setup/pricing",
            defaults: new { area = "SystemAdmin", controller = "Setup", action = "Pricing" });

        app.MapControllerRoute(
            name: "systemAdmin-actions",
            pattern: "systemAdmin/{action=Index}/{id?}",
            defaults: new { area = "SystemAdmin", controller = "SystemAdmin", action = "Index" });
    }
}
