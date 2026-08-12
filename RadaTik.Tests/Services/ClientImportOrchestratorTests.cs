using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using RadaTik.Services.PricingPolicies;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientImportOrchestratorTests
{
    [Fact]
    public async Task ExecuteImportAsync_NoImportableUsers_ReturnsFailed()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(new MikroTikServer
        {
            Id = 1,
            Name = "S1",
            NetworkId = 2,
            Host = "1.1.1.1",
            User = "admin",
            Pass = "pass"
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikUserImportService> import = new(MockBehavior.Strict);
        import
            .Setup(i => i.BuildUsersImportPreviewAsync(1, 2))
            .ReturnsAsync(new ImportUsersPreviewResult { ImportableUsersCount = 0 });

        Mock<IUsageBasedSubscriptionChargeService> charges = new(MockBehavior.Strict);
        ClientImportOrchestrator sut = new(db, import.Object, charges.Object);

        ClientImportOutcome outcome = await sut.ExecuteImportAsync(1, 2, "actor-1", rejectWhenProfilesMissing: false);

        Assert.False(outcome.Success);
        import.Verify(i => i.ImportAllUsersToDatabase(It.IsAny<int>(), It.IsAny<int>()), Times.Never);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
