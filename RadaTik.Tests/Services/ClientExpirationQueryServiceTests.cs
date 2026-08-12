using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientExpirationQueryServiceTests
{
    [Fact]
    public async Task BuildExpiredAccountsPageAsync_CountsActiveAndDisabled()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Profiles.Add(new Profile { Id = 1, Name = "P1", NetworkId = 2 });
        DateTime yesterday = DateTime.Now.Date.AddDays(-1);
        db.Clients.AddRange(
            Client(1, yesterday, true),
            Client(2, yesterday, false));
        await db.SaveChangesAsync();

        ClientExpirationQueryService sut = new(db);
        ClientExpiredAccountsPageModel page = await sut.BuildExpiredAccountsPageAsync(2);

        Assert.Equal(2, page.TotalExpired);
        Assert.Equal(1, page.ActiveExpired);
        Assert.Equal(1, page.DisabledExpired);
    }

    private static Client Client(int id, DateTime expiration, bool isActive) => new()
    {
        Id = id,
        Name = $"c{id}",
        UserName = $"u{id}",
        Password = "p",
        SID = "1",
        PhoneNumber = "0",
        ProfileId = 1,
        NetworkId = 2,
        AccountExpirationDate = expiration,
        IsActive = isActive
    };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
