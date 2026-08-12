using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using RadaTik.Areas.SystemAdmin.ViewModels;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ServiceCatalogFormParserTests
{
    [Fact]
    public void Parse_ReadsNestedFields_WithInvariantDecimalFormat()
    {
        FormCollection form = BuildForm(new Dictionary<string, StringValues>
        {
            ["Network.InitialPriceSyp"] = "1500.50",
            ["Network.RenewalBillingPeriod"] = "1",
            ["Network.RenewalPricePerUnitSyp"] = "250,75",
            ["Network.FreeInitialUnits"] = "2",
            ["Network.FreeRenewalUnits"] = "3",
            ["ReportInitialPriceSyp"] = "100",
            ["MaintenanceCommissionMode"] = "1",
            ["MaintenanceCommissionValue"] = "5.5",
            ["ProfileTaxPercentage"] = "15"
        });

        ServiceCatalogSaveViewModel model = ServiceCatalogFormParser.Parse(form);

        Assert.Equal(1500.50m, model.Network.InitialPriceSyp);
        Assert.Equal(PricingBillingPeriod.Monthly, model.Network.RenewalBillingPeriod);
        Assert.Equal(250.75m, model.Network.RenewalPricePerUnitSyp);
        Assert.Equal(2, model.Network.FreeInitialUnits);
        Assert.Equal(3, model.Network.FreeRenewalUnits);
        Assert.Equal(100m, model.ReportInitialPriceSyp);
        Assert.Equal(MaintenanceCommissionMode.Percent, model.MaintenanceCommissionMode);
        Assert.Equal(5.5m, model.MaintenanceCommissionValue);
        Assert.Equal(15m, model.ProfileTaxPercentage);
    }

    private static FormCollection BuildForm(Dictionary<string, StringValues> values)
    {
        IFormCollection form = new FormCollection(values);
        return (FormCollection)form;
    }
}
