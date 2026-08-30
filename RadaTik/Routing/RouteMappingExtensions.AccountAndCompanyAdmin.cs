namespace RadaTik.Routing;

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
            name: "reglog-app-update-required",
            pattern: "Account/AppUpdateRequired",
            defaults: new { controller = "Account", action = "AppUpdateRequired" });

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

        app.MapControllerRoute(
            name: "reglog-register-network-admin",
            pattern: "Account/RegisterNetworkAdmin",
            defaults: new { controller = "Account", action = "RegisterNetworkAdmin" });
    }

    private static void MapCompanyAdminRoutes(this WebApplication app)
    {
        app.MapControllerRoute(
            name: "networkManager-validate-newEmployee",
            pattern: "networkManager/Users/Employee/addnewEmployee/validate",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "ValidateCreateEmployeeAccount" });

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
            name: "networkManager-wallet-index",
            pattern: "networkManager/wallet",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-wallet-fund-from-cashbox-obsolete",
            pattern: "networkManager/wallet/fund-from-cashbox",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "ObsoleteFundFromCashBoxRedirect" });

        app.MapControllerRoute(
            name: "networkManager-wallet-topup",
            pattern: "networkManager/wallet/topup",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "TopUp" });

        app.MapControllerRoute(
            name: "networkManager-wallet-transactions",
            pattern: "networkManager/wallet/transactions",
            defaults: new { area = "CompanyAdmin", controller = "Wallet", action = "Transactions" });

        app.MapControllerRoute(
            name: "networkManager-client-wallet-topups",
            pattern: "networkManager/wallet/client-topups/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "ClientWalletTopUpRequests", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-employee-wallet-topups",
            pattern: "networkManager/wallet/employee-topups/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "EmployeeWalletTopUpRequests", action = "Index" });

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
            name: "networkManager-company-business",
            pattern: "networkManager/CompanyBusiness/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "CompanyBusiness", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-private-subscriber-setup",
            pattern: "networkManager/subscriber-setup/private/{action=Create}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "PrivateSubscriberSetup", action = "Create" });

        app.MapControllerRoute(
            name: "networkManager-new-subscriber-wizard",
            pattern: "networkManager/Clients/wizard/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "NewSubscriberWizard", action = "Index" });

        // مسار موظف الشركة تحت /employee حتى لا يُرفض عبر AreaIsolation
        app.MapControllerRoute(
            name: "employee-new-subscriber-wizard",
            pattern: "employee/Clients/wizard/{action=Index}/{id?}",
            defaults: new { area = "CompanyEmployee", controller = "NewSubscriberWizard", action = "Index" });

        // توافق مع الروابط القديمة /CompanyEmployee/Clients/wizard → نفس الـ controller
        app.MapControllerRoute(
            name: "companyEmployee-new-subscriber-wizard-legacy",
            pattern: "CompanyEmployee/Clients/wizard/{action=Index}/{id?}",
            defaults: new { area = "CompanyEmployee", controller = "NewSubscriberWizard", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-warehouse",
            pattern: "networkManager/Warehouse/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Warehouse", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-material-purchase",
            pattern: "networkManager/MaterialPurchase/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MaterialPurchaseInvoices", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-material-sales",
            pattern: "networkManager/MaterialSales/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MaterialSalesInvoices", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-warehouse-stocktake",
            pattern: "networkManager/WarehouseStocktake/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "WarehouseStocktake", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-money-diary",
            pattern: "networkManager/MoneyDiary/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MoneyDiary", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-financial-reconciliation",
            pattern: "networkManager/FinancialReconciliation/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "FinancialReconciliation", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-payroll",
            pattern: "networkManager/Payroll/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "Payroll", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-maintenance-invoices",
            pattern: "networkManager/MaintenanceInvoices/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MaintenanceInvoices", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-maintenance-pricing",
            pattern: "networkManager/MaintenancePricing/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "MaintenancePricing", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-employee-approvals",
            pattern: "networkManager/EmployeeServiceApprovals/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "EmployeeServiceApprovals", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-employee-details",
            pattern: "networkManager/Users/Employee/details/{id}",
            defaults: new { area = "CompanyAdmin", controller = "Admin", action = "DetailsEmployee" });

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
            name: "networkManager-subscriber-installation-pricing",
            pattern: "networkManager/SubscriberInstallationPricing/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "SubscriberInstallationPricing", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-subscriber-installation-invoices",
            pattern: "networkManager/SubscriberInstallationInvoices/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "SubscriberInstallationInvoices", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-renewalRequests",
            pattern: "networkManager/RenewalRequests/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "RenewalRequests", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-dashboard",
            pattern: "networkManager/dashboard",
            defaults: new { area = "CompanyAdmin", controller = "Dashboard", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-operations-hub",
            pattern: "networkManager/operations",
            defaults: new { area = "CompanyAdmin", controller = "Dashboard", action = "Operations" });

        app.MapControllerRoute(
            name: "networkManager-onboarding-dismiss",
            pattern: "networkManager/onboarding/dismiss",
            defaults: new { area = "CompanyAdmin", controller = "Onboarding", action = "Dismiss" });

        app.MapControllerRoute(
            name: "networkManager-setup-requiredPassword",
            pattern: "networkManager/setup/requiredPassword",
            defaults: new { area = "CompanyAdmin", controller = "Setup", action = "RequiredPassword" });

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

        app.MapControllerRoute(
            name: "networkManager-erp",
            pattern: "networkManager/erp",
            defaults: new { area = "CompanyAdmin", controller = "ErpHub", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-erp-customers",
            pattern: "networkManager/erp/customers/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "ErpCustomers", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-erp-suppliers",
            pattern: "networkManager/erp/suppliers/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "ErpSuppliers", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-erp-tasks",
            pattern: "networkManager/erp/tasks/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "CompanyEmployeeTasks", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-erp-rewards",
            pattern: "networkManager/erp/rewards/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "EmployeeRewardPenalties", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-erp-accounting",
            pattern: "networkManager/erp/accounting/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "CompanyAccounting", action = "Index" });

        app.MapControllerRoute(
            name: "networkManager-erp-reports",
            pattern: "networkManager/erp/reports/{action=Index}/{id?}",
            defaults: new { area = "CompanyAdmin", controller = "ErpReports", action = "Index" });

        app.MapAreaControllerRoute(
            name: "networkManager-area",
            areaName: "CompanyAdmin",
            pattern: "networkManager/{controller=Dashboard}/{action=Index}/{id?}");
    }
}
