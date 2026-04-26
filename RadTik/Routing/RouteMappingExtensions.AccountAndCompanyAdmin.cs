namespace RadTik.Routing;

public static partial class RouteMappingExtensions
{
    private static void MapAccountRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "reglog-login",
            pattern: "Account/login",
            defaults: new { controller = "Account", action = "Login" });

        app.MapControllerRoute(
            name: "reglog-logout",
            pattern: "Account/logout",
            defaults: new { controller = "Account", action = "Logout" });

        app.MapControllerRoute(
            name: "reglog-accessdenied",
            pattern: "Account/accessDenied",
            defaults: new { controller = "Account", action = "AccessDenied" });

        app.MapControllerRoute(
            name: "reglog-forgotpassword",
            pattern: "Account/forgotPassword",
            defaults: new { controller = "Account", action = "ForgotPassword" });

        app.MapControllerRoute(
            name: "reglog-verifyResetCode",
            pattern: "Account/verifyResetCode",
            defaults: new { controller = "Account", action = "VerifyResetCode" });

        app.MapControllerRoute(
            name: "reglog-resetPassword",
            pattern: "Account/resetPassword",
            defaults: new { controller = "Account", action = "ResetPassword" });
    }

    private static void MapCompanyAdminRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "networkManager-addnewEmployee",
            pattern: "networkManager/Users/Employee/addnewEmployee",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "CreateEmployee" });

        app.MapControllerRoute(
            name: "networkManager-users",
            pattern: "networkManager/Users",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-features",
            pattern: "networkManager/features",
            defaults: new { area = "CompanyAdmin", controller = "Features", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-wallet-topup",
            pattern: "networkManager/wallet/topup",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "TopUp" });

        app.MapControllerRoute(
            name: "networkManager-wallet-transactions",
            pattern: "networkManager/wallet/transactions",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "Transactions" });

        app.MapControllerRoute(
            name: "networkManager-wallet-howto",
            pattern: "networkManager/wallet/howto",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "TopUp" });

        app.MapControllerRoute(
            name: "networkManager-receipts",
            pattern: "networkManager/receipts/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Receipts", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-cashbox",
            pattern: "networkManager/CashBox/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "CashBox", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-customService-index",
            pattern: "networkManager/service/{serviceKey}",
            defaults: new { area = "CompanyAdmin", controller = "CustomServices", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-customService-create",
            pattern: "networkManager/service/{serviceKey}/create",
            defaults: new { area = "CompanyAdmin", controller = "CustomServices", action = "Create" });

        app.MapControllerRoute(
            name: "networkManager-customService-edit",
            pattern: "networkManager/service/edit/{id:int}",
            defaults: new { area = "CompanyAdmin", controller = "CustomServices", action = "Edit" });

        app.MapControllerRoute(
            name: "networkManager-notifications",
            pattern: "networkManager/notifications",
            defaults: new { area = "CompanyAdmin", controller = "Notifications", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-users-employees",
            pattern: "networkManager/Users/Employee",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "Index", type = "employees" });

        app.MapControllerRoute(
            name: "networkManager-users-clients",
            pattern: "networkManager/Users/Client",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "Index", type = "clients" });

        app.MapControllerRoute(
            name: "networkManager-users-collectionPoints",
            pattern: "networkManager/Users/CollectionPoint",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "Index", type = "points" });

        app.MapControllerRoute(
            name: "networkManager-employee-edit",
            pattern: "networkManager/Users/Employee/edit/{id}",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "EditEmployee" });

        app.MapControllerRoute(
            name: "networkManager-employee-delete",
            pattern: "networkManager/Users/Employee/delete/{id}",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "DeleteEmployee" });

        app.MapControllerRoute(
            name: "networkManager-mikrotikservers",
            pattern: "networkManager/MikroTikServers/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MikroTikServers", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-mikrotik-traffic",
            pattern: "networkManager/MikroTikTraffic/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MikroTikTraffic", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-network",
            pattern: "networkManager/Network/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Network", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-profile",
            pattern: "networkManager/Profile/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Profile", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-sector",
            pattern: "networkManager/Sector/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Sector", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-receiver",
            pattern: "networkManager/Receiver/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Receiver", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-collectionpoints",
            pattern: "networkManager/CollectionPoints/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "CollectionPoints", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-clients",
            pattern: "networkManager/Clients/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Clients", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-requestsManagement",
            pattern: "networkManager/RequestsManagement/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "RequestsManagement", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-renewalRequests",
            pattern: "networkManager/RenewalRequests/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "RenewalRequests", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-dashboard",
            pattern: "networkManager/dashboard",
            defaults: new { area = "CompanyAdmin", controller = "Dashboard", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-reports",
            pattern: "networkManager/Reports/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Reports", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-joinRequests-passwordResets",
            pattern: "networkManager/JoinRequests/{action=PasswordResets}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "JoinRequests", action = "PasswordResets" });

        app.MapControllerRoute(
            name: "networkManager-account-profile",
            pattern: "networkManager/Account/profile",
            defaults: new { area = "CompanyAdmin", controller = "Account", action = "Profile" });

        app.MapControllerRoute(
            name: "networkManager-account",
            pattern: "networkManager/Account/{action=Profile}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Account", action = "Profile" });

        app.MapAreaControllerRoute(
            name: "networkManager-area",
            areaName: "CompanyAdmin",
            pattern: "networkManager/{controller=Dashboard}/{action=Index}/{id?}");
    }
}
