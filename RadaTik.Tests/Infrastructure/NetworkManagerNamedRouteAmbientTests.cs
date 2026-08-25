using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace RadaTik.Tests.Infrastructure;

/// <summary>
/// Verifies named networkManager routes resolve to the expected list/detail paths.
/// Sidebar/hero links pass asp-route-action="Index" asp-route-id="" to avoid ambient Details/id pollution.
/// </summary>
public class NetworkManagerNamedRouteAmbientTests : IClassFixture<RadaTikWebApplicationFactory>
{
    private readonly RadaTikWebApplicationFactory _factory;

    public NetworkManagerNamedRouteAmbientTests(RadaTikWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("networkManager-clients", "Index", "/networkManager/Clients")]
    [InlineData("networkManager-cashbox", "Index", "/networkManager/CashBox")]
    [InlineData("networkManager-warehouse", "Index", "/networkManager/Warehouse")]
    [InlineData("networkManager-payroll", "Index", "/networkManager/Payroll")]
    [InlineData("networkManager-reports", "Index", "/networkManager/Reports")]
    [InlineData("networkManager-mikrotikservers", "Index", "/networkManager/MikroTikServers")]
    [InlineData("networkManager-sector", "Index", "/networkManager/Sector")]
    [InlineData("networkManager-receiver", "Index", "/networkManager/Receiver")]
    [InlineData("networkManager-company-business", "Index", "/networkManager/CompanyBusiness")]
    [InlineData("networkManager-money-diary", "Index", "/networkManager/MoneyDiary")]
    [InlineData("networkManager-financial-reconciliation", "Index", "/networkManager/FinancialReconciliation")]
    [InlineData("networkManager-material-purchase", "Index", "/networkManager/MaterialPurchase")]
    [InlineData("networkManager-material-sales", "Index", "/networkManager/MaterialSales")]
    [InlineData("networkManager-warehouse-stocktake", "Index", "/networkManager/WarehouseStocktake")]
    [InlineData("networkManager-client-wallet-topups", "Index", "/networkManager/wallet/client-topups")]
    [InlineData("networkManager-employee-wallet-topups", "Index", "/networkManager/wallet/employee-topups")]
    [InlineData("networkManager-maintenance-invoices", "Index", "/networkManager/MaintenanceInvoices")]
    [InlineData("networkManager-maintenance-pricing", "Index", "/networkManager/MaintenancePricing")]
    [InlineData("networkManager-maintenance-pricing", "SaveAll", "/networkManager/MaintenancePricing/SaveAll")]
    [InlineData("networkManager-subscriber-installation-invoices", "Index", "/networkManager/SubscriberInstallationInvoices")]
    [InlineData("networkManager-subscriber-installation-pricing", "Index", "/networkManager/SubscriberInstallationPricing")]
    [InlineData("networkManager-erp-customers", "Index", "/networkManager/erp/customers")]
    [InlineData("networkManager-erp-reports", "Index", "/networkManager/erp/reports")]
    [InlineData("networkManager-employee-approvals", "Index", "/networkManager/EmployeeServiceApprovals")]
    public void Index_named_routes_resolve_to_list_paths(string routeName, string action, string expectedPath)
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        LinkGenerator linkGenerator = scope.ServiceProvider.GetRequiredService<LinkGenerator>();

        string? url = linkGenerator.GetPathByName(routeName, values: new { action });
        Assert.Equal(expectedPath, url);
    }

    [Fact]
    public void Static_and_detail_named_routes_are_stable()
    {
        using IServiceScope scope = _factory.Services.CreateScope();
        LinkGenerator linkGenerator = scope.ServiceProvider.GetRequiredService<LinkGenerator>();

        Assert.Equal("/networkManager/wallet", linkGenerator.GetPathByName("networkManager-wallet-index", values: null));
        Assert.Equal("/networkManager/wallet/topup", linkGenerator.GetPathByName("networkManager-wallet-topup", values: null));
        Assert.Equal("/networkManager/wallet/transactions", linkGenerator.GetPathByName("networkManager-wallet-transactions", values: null));
        Assert.Equal("/networkManager/dashboard", linkGenerator.GetPathByName("networkManager-dashboard", values: null));
        Assert.Equal("/networkManager/Users/Employee", linkGenerator.GetPathByName("networkManager-users-employees", values: null));
        Assert.Equal("/networkManager/Users/Employee/details/7", linkGenerator.GetPathByName("networkManager-employee-details", values: new { id = 7 }));
        Assert.Equal("/networkManager/MikroTikServers/Details/5", linkGenerator.GetPathByName("networkManager-mikrotikservers", values: new { action = "Details", id = 5 }));
    }
}
