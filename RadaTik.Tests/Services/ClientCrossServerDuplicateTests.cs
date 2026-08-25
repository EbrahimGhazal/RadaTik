using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientCrossServerDuplicateTests
{
    [Fact]
    public async Task RemoveCopiesMissingFromServer_DeletesStaleDuplicateAndClearsRemaining()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(Client(1, "same-user", 2, serverId: 9, isDuplicate: true));
        db.Clients.Add(Client(2, "same-user", 2, serverId: 10, isDuplicate: true));
        await db.SaveChangesAsync();

        int removed = await ClientCrossServerDuplicate.RemoveCopiesMissingFromServerAsync(
            db,
            networkId: 2,
            serverId: 10,
            liveUserNamesOnServer: Array.Empty<string>());
        await db.SaveChangesAsync();

        Assert.Equal(1, removed);
        Client remaining = Assert.Single(await db.Clients.ToListAsync());
        Assert.Equal(1, remaining.Id);
        Assert.False(remaining.IsCrossServerDuplicate);
    }

    [Fact]
    public async Task RemoveCopiesMissingFromServer_DoesNotDeleteWhenStillOnTower()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(Client(1, "same-user", 2, serverId: 9, isDuplicate: true));
        db.Clients.Add(Client(2, "same-user", 2, serverId: 10, isDuplicate: true));
        await db.SaveChangesAsync();

        int removed = await ClientCrossServerDuplicate.RemoveCopiesMissingFromServerAsync(
            db,
            networkId: 2,
            serverId: 10,
            liveUserNamesOnServer: ["same-user"]);
        await db.SaveChangesAsync();

        Assert.Equal(0, removed);
        Assert.Equal(2, await db.Clients.CountAsync());
        Assert.All(await db.Clients.ToListAsync(), c => Assert.True(c.IsCrossServerDuplicate));
    }

    [Fact]
    public async Task RemoveCopiesMissingFromServer_DoesNotDeleteSoleClientMissingFromMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(Client(1, "solo", 2, serverId: 10, isDuplicate: true));
        await db.SaveChangesAsync();

        int removed = await ClientCrossServerDuplicate.RemoveCopiesMissingFromServerAsync(
            db,
            networkId: 2,
            serverId: 10,
            liveUserNamesOnServer: Array.Empty<string>());
        await db.SaveChangesAsync();

        Assert.Equal(0, removed);
        Assert.Equal(1, await db.Clients.CountAsync());
    }

    private static Client Client(int id, string userName, int networkId, int serverId, bool isDuplicate) =>
        new()
        {
            Id = id,
            Name = userName,
            UserName = userName,
            Password = "pass",
            SID = "1234567890",
            PhoneNumber = "0999999999",
            NetworkId = networkId,
            ProfileId = 1,
            MikroTikServerId = serverId,
            IsCrossServerDuplicate = isDuplicate
        };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
