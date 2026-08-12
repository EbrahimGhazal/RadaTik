using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RadaTik.Areas.SystemAdmin.Controllers;
using RadaTik.Areas.SystemAdmin.ViewModels;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.SystemAdminPricing;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ServiceCatalogControllerSaveTests
{
    [Fact]
    public async Task SaveAllPricing_PersistsNetworkPricingAndRedirectsWithSavedFlag()
    {
        await using ApplicationDbContext db = CreateDbContext();
        ServiceCatalogController controller = BuildController(db);

        DefaultHttpContext httpContext = new();
        httpContext.Request.Form = new FormCollection(new Dictionary<string, Microsoft.Extensions.Primitives.StringValues>
        {
            ["Network.InitialPriceSyp"] = "1500",
            ["Network.RenewalBillingPeriod"] = "1",
            ["Network.RenewalPricePerUnitSyp"] = "300",
            ["Network.FreeInitialUnits"] = "0",
            ["Network.FreeRenewalUnits"] = "0",
            ["Server.InitialPriceSyp"] = "100",
            ["Server.RenewalBillingPeriod"] = "1",
            ["Server.RenewalPricePerUnitSyp"] = "20",
            ["Server.FreeInitialUnits"] = "0",
            ["Server.FreeRenewalUnits"] = "0",
            ["Sector.InitialPriceSyp"] = "100",
            ["Sector.RenewalBillingPeriod"] = "1",
            ["Sector.RenewalPricePerUnitSyp"] = "20",
            ["Sector.FreeInitialUnits"] = "0",
            ["Sector.FreeRenewalUnits"] = "0",
            ["Receiver.InitialPriceSyp"] = "100",
            ["Receiver.RenewalBillingPeriod"] = "1",
            ["Receiver.RenewalPricePerUnitSyp"] = "20",
            ["Receiver.FreeInitialUnits"] = "0",
            ["Receiver.FreeRenewalUnits"] = "0",
            ["Client.InitialPriceSyp"] = "100",
            ["Client.RenewalBillingPeriod"] = "1",
            ["Client.RenewalPricePerUnitSyp"] = "20",
            ["Client.FreeInitialUnits"] = "0",
            ["Client.FreeRenewalUnits"] = "0",
            ["User.InitialPriceSyp"] = "100",
            ["User.RenewalBillingPeriod"] = "1",
            ["User.RenewalPricePerUnitSyp"] = "20",
            ["User.FreeInitialUnits"] = "0",
            ["User.FreeRenewalUnits"] = "0",
            ["SpeedProfile.InitialPriceSyp"] = "100",
            ["SpeedProfile.RenewalBillingPeriod"] = "1",
            ["SpeedProfile.RenewalPricePerUnitSyp"] = "20",
            ["SpeedProfile.FreeInitialUnits"] = "0",
            ["SpeedProfile.FreeRenewalUnits"] = "0",
            ["ReportInitialPriceSyp"] = "50",
            ["MaintenanceCommissionMode"] = "0",
            ["MaintenanceCommissionValue"] = "10",
            ["ProfileTaxPercentage"] = "15"
        });

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        IActionResult result = await controller.SaveAllPricing();

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal(1, redirect.RouteValues?["saved"]);

        FeaturePricing? networkInitial = await db.FeaturePricings.FirstOrDefaultAsync(p =>
            p.FeatureKey == FeatureKeys.Networks &&
            p.ChargeUnit == PricingChargeUnit.PerNetwork &&
            p.BillingPeriod == PricingBillingPeriod.OneTime &&
            p.IsActive);

        Assert.NotNull(networkInitial);
        Assert.Equal(1500m, networkInitial!.AmountSYP);
    }

    private static ServiceCatalogController BuildController(ApplicationDbContext db)
    {
        IEnumerable<IRecurringServicePricingHandler> recurringHandlers =
        [
            new NetworkRecurringPricingHandler(db, NullLogger<NetworkRecurringPricingHandler>.Instance),
            new ServerRecurringPricingHandler(db, NullLogger<ServerRecurringPricingHandler>.Instance),
            new SectorRecurringPricingHandler(db, NullLogger<SectorRecurringPricingHandler>.Instance),
            new ReceiverRecurringPricingHandler(db, NullLogger<ReceiverRecurringPricingHandler>.Instance),
            new ClientRecurringPricingHandler(db, NullLogger<ClientRecurringPricingHandler>.Instance),
            new UserRecurringPricingHandler(db, NullLogger<UserRecurringPricingHandler>.Instance),
            new SpeedProfileRecurringPricingHandler(db, NullLogger<SpeedProfileRecurringPricingHandler>.Instance)
        ];

        IRecurringServicePricingHandlerResolver recurringResolver = new RecurringServicePricingHandlerResolver(recurringHandlers);
        IStandaloneServicePricingHandlerResolver standaloneResolver = new StandaloneServicePricingHandlerResolver(
            new ReportPricingHandler(db, NullLogger<ReportPricingHandler>.Instance),
            new ProfileTaxPricingHandler(db, NullLogger<ProfileTaxPricingHandler>.Instance),
            new MaintenanceCommissionPricingHandler(db, NullLogger<MaintenanceCommissionPricingHandler>.Instance));

        return new ServiceCatalogController(
            db,
            NullLogger<ServiceCatalogController>.Instance,
            recurringResolver,
            standaloneResolver,
            new ServiceCatalogSnapshotProvider(db),
            new SystemAdminPricingReadinessService(new ServiceCatalogSnapshotProvider(db)));
    }

    private static ApplicationDbContext CreateDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
