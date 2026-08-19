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

    [Fact]
    public async Task BuildImportPageAsync_OfflineServer_DoesNotFailOtherServers()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.AddRange(
            new MikroTikServer
            {
                Id = 11,
                Name = "A-Down",
                NetworkId = 2,
                Host = "10.0.0.1",
                User = "admin",
                Pass = "pass"
            },
            new MikroTikServer
            {
                Id = 12,
                Name = "B-Up",
                NetworkId = 2,
                Host = "10.0.0.2",
                User = "admin",
                Pass = "pass"
            });
        await db.SaveChangesAsync();

        Mock<IMikroTikUserImportService> import = new();
        import.Setup(i => i.BuildUsersImportPreviewAsync(11, 2))
            .ThrowsAsync(new InvalidOperationException("connection refused"));
        import.Setup(i => i.BuildUsersImportPreviewAsync(12, 2))
            .ReturnsAsync(new ImportUsersPreviewResult { ImportableUsersCount = 4, TotalUsersOnServer = 4 });

        Mock<IUsageBasedSubscriptionChargeService> charges = CreateChargeMock();
        ClientImportOrchestrator sut = new(db, import.Object, charges.Object, serverPreviewTimeout: TimeSpan.FromMilliseconds(250));
        ClientImportPageModel page = await sut.BuildImportPageAsync(2);

        Assert.True(page.PreviewByServer[11].HasConnectionError);
        Assert.Contains("تخطي", page.PreviewByServer[11].PreviewNote);
        Assert.False(page.PreviewByServer[12].HasConnectionError);
        Assert.Equal(4, page.PreviewByServer[12].ImportableUsersCount);
    }

    [Fact]
    public async Task BuildImportPageAsync_UnresponsiveServer_SkipsAndContinues()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.AddRange(
            new MikroTikServer
            {
                Id = 21,
                Name = "A-Hang",
                NetworkId = 2,
                Host = "10.0.0.1",
                User = "admin",
                Pass = "pass"
            },
            new MikroTikServer
            {
                Id = 22,
                Name = "B-Up",
                NetworkId = 2,
                Host = "10.0.0.2",
                User = "admin",
                Pass = "pass"
            });
        await db.SaveChangesAsync();

        Mock<IMikroTikUserImportService> import = new();
        import.Setup(i => i.BuildUsersImportPreviewAsync(21, 2))
            .Returns(async () =>
            {
                await Task.Delay(TimeSpan.FromSeconds(5));
                return new ImportUsersPreviewResult { ImportableUsersCount = 9 };
            });
        import.Setup(i => i.BuildUsersImportPreviewAsync(22, 2))
            .ReturnsAsync(new ImportUsersPreviewResult { ImportableUsersCount = 2, TotalUsersOnServer = 2 });

        Mock<IUsageBasedSubscriptionChargeService> charges = CreateChargeMock();
        ClientImportOrchestrator sut = new(db, import.Object, charges.Object, serverPreviewTimeout: TimeSpan.FromMilliseconds(200));

        ClientImportPageModel page = await sut.BuildImportPageAsync(2);

        Assert.True(page.PreviewByServer[21].HasConnectionError);
        Assert.Contains("خارج الخدمة", page.PreviewByServer[21].PreviewNote);
        Assert.Equal(2, page.PreviewByServer[22].ImportableUsersCount);
    }

    [Fact]
    public async Task ExecuteImportAsync_OfflineServer_ReturnsSkippedWithoutImport()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(new MikroTikServer
        {
            Id = 31,
            Name = "Down",
            NetworkId = 2,
            Host = "10.0.0.1",
            User = "admin",
            Pass = "pass"
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikUserImportService> import = new();
        import.Setup(i => i.BuildUsersImportPreviewAsync(31, 2))
            .ThrowsAsync(new TimeoutException("timed out"));

        Mock<IUsageBasedSubscriptionChargeService> charges = CreateChargeMock();
        ClientImportOrchestrator sut = new(db, import.Object, charges.Object, serverPreviewTimeout: TimeSpan.FromMilliseconds(200));

        ClientImportOutcome outcome = await sut.ExecuteImportAsync(31, 2, "actor-1", rejectWhenProfilesMissing: false);

        Assert.False(outcome.Success);
        Assert.True(outcome.Skipped);
        import.Verify(i => i.ImportAllUsersToDatabase(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    private static Mock<IUsageBasedSubscriptionChargeService> CreateChargeMock()
    {
        Mock<IUsageBasedSubscriptionChargeService> charges = new();
        charges.Setup(c => c.EstimateImportChargeAsync(
                It.IsAny<int>(),
                PricingChargeUnit.PerSubscriber,
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((int _, PricingChargeUnit _, int count, CancellationToken _) =>
                new UsageImportChargeEstimate
                {
                    UnitPriceSyp = 1000m,
                    RequiredAmountSyp = 1000m * count,
                    ImportableCount = count
                });
        return charges;
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"RadaTik_Import_{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options);
    }
}
