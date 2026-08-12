using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientFormLookupServiceTests
{
    [Fact]
    public async Task GetProfilesByServerAsync_FiltersByNetworkAndServer()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(new MikroTikServer { Id = 5, Name = "S", NetworkId = 2, Host = "h", User = "u", Pass = "p" });
        db.Profiles.Add(new Profile { Id = 10, Name = "P", NetworkId = 2, MikroTikServerId = 5, IsActive = true });
        db.Profiles.Add(new Profile { Id = 11, Name = "Other", NetworkId = 99, MikroTikServerId = 5, IsActive = true });
        await db.SaveChangesAsync();

        ClientFormLookupService sut = new(db);
        IReadOnlyList<ClientFormProfileOption> profiles = await sut.GetProfilesByServerAsync(5, 2);

        Assert.Single(profiles);
        Assert.Equal(10, profiles[0].Id);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
