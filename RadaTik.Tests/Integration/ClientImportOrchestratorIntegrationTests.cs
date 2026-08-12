using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPolicies;
using Xunit;

namespace RadaTik.Tests.Integration;

public sealed class ClientImportOrchestratorIntegrationTests
{
    [Fact]
    public async Task BuildImportFromServerViewAsync_ReturnsPreviewPerServer()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(new MikroTikServer
        {
            Id = 10,
            Name = "SRV",
            NetworkId = 2,
            Host = "10.0.0.1",
            User = "admin",
            Pass = "pass"
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikUserImportService> import = new();
        import.Setup(i => i.BuildUsersImportPreviewAsync(10, 2))
            .ReturnsAsync(new ImportUsersPreviewResult { ImportableUsersCount = 3 });

        Mock<IUsageBasedSubscriptionChargeService> charges = new();
        charges.Setup(c => c.EstimateImportChargeAsync(2, PricingChargeUnit.PerSubscriber, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageImportChargeEstimate { UnitPriceSyp = 1000m, RequiredAmountSyp = 3000m });
        charges.Setup(c => c.EstimateImportChargeAsync(2, PricingChargeUnit.PerSubscriber, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UsageImportChargeEstimate { UnitPriceSyp = 1000m });

        ClientImportOrchestrator sut = new(db, import.Object, charges.Object);
        ClientImportFromServerViewModel view = await sut.BuildImportFromServerViewAsync(2);

        Assert.Single(view.Servers);
        Assert.Equal(3, view.ImportPage.PreviewByServer[10].ImportableUsersCount);
        Assert.Equal(1000m, view.ImportPage.SubscriberUnitPrice);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"RadaTik_Import_{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
