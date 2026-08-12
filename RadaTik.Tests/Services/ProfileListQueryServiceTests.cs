using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Profiles;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ProfileListQueryServiceTests
{
    [Fact]
    public async Task BuildIndexPageAsync_ReturnsStats()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 2, Name = "Net" });
        db.MikroTikServers.Add(new MikroTikServer { Id = 5, Name = "S", NetworkId = 2, Host = "h", User = "u", Pass = "p" });
        db.Profiles.Add(new Profile { Id = 1, Name = "P1", NetworkId = 2, MikroTikServerId = 5, IsActive = true, IsSyncedWithMikroTik = true });
        await db.SaveChangesAsync();

        Mock<IProfileImportPricingService> pricing = new();
        pricing.Setup(p => p.GetProfileImportUnitPriceAsync(It.IsAny<CancellationToken>())).ReturnsAsync(500m);

        ProfileListQueryService sut = new(db, pricing.Object);
        ProfileIndexPageModel? page = await sut.BuildIndexPageAsync(2, null);

        Assert.NotNull(page);
        Assert.Single(page!.Profiles);
        Assert.Equal(1, page.TotalProfiles);
        Assert.Equal(1, page.ActiveProfiles);
        Assert.Equal(1, page.SyncedProfiles);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
