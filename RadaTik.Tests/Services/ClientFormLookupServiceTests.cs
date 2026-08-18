using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientFormLookupServiceTests
{
    [Fact]
    public async Task GetProfilesByServerAsync_ReturnsActiveSyncedProfilesForTower()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(new MikroTikServer { Id = 5, Name = "S", NetworkId = 2, Host = "h", User = "u", Pass = "p" });
        db.MikroTikServers.Add(new MikroTikServer { Id = 6, Name = "Other", NetworkId = 2, Host = "h2", User = "u", Pass = "p" });
        db.Profiles.Add(new Profile { Id = 10, Name = "P", NetworkId = 2, MikroTikServerId = 5, IsActive = true });
        db.Profiles.Add(new Profile { Id = 11, Name = "SyncedOtherNet", NetworkId = 99, MikroTikServerId = 5, IsActive = true });
        db.Profiles.Add(new Profile { Id = 12, Name = "Inactive", NetworkId = 2, MikroTikServerId = 5, IsActive = false });
        db.Profiles.Add(new Profile { Id = 13, Name = "OtherTower", NetworkId = 2, MikroTikServerId = 6, IsActive = true });
        await db.SaveChangesAsync();

        ClientFormLookupService sut = new(db);
        IReadOnlyList<ClientFormProfileOption> profiles = await sut.GetProfilesByServerAsync(5, 2);

        Assert.Equal(2, profiles.Count);
        Assert.Contains(profiles, p => p.Id == 10);
        Assert.Contains(profiles, p => p.Id == 11);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
