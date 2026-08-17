using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientMikroTikLifecycleServiceCopyToTowerTests
{
    [Fact]
    public async Task BulkCopyAccountsToServerAsync_NoSelection_ReturnsFail()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(Server(5, 10));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);

        BulkCopyAccountsToServerResult result = await sut.BulkCopyAccountsToServerAsync(
            10, 5, Array.Empty<int>(), applyToAllInNetwork: false);

        Assert.False(result.Success);
        Assert.Contains("تحديد", result.Message);
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BulkCopyAccountsToServerAsync_ServerOutsideNetwork_ReturnsFail()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(Server(5, 99));
        db.Clients.Add(MinimalClient(1, "u1", 10));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);

        BulkCopyAccountsToServerResult result = await sut.BulkCopyAccountsToServerAsync(
            10, 5, [1], applyToAllInNetwork: false);

        Assert.False(result.Success);
        Assert.Contains("السيرفر", result.Message);
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BulkCopyAccountsToServerAsync_SelectedClients_MovesToNewTower()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(Server(5, 10));
        db.MikroTikServers.Add(Server(2, 10));
        db.Clients.Add(MinimalClient(1, "user-a", 10, 2));
        db.Clients.Add(MinimalClient(2, "user-b", 10, 2));
        db.Clients.Add(MinimalClient(3, "other-net", 11, 9));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.AddPPPoEUsersToServerAsync(
                5,
                It.Is<IReadOnlyList<Client>>(list =>
                    list.Count == 2
                    && list.Any(c => c.UserName == "user-a")
                    && list.Any(c => c.UserName == "user-b")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkAddPppoeUsersResult
            {
                Success = true,
                AddedCount = 2,
                PlacedClientIds = [1, 2],
                Message = "ok"
            });
        mikroTik
            .Setup(m => m.DeletePPPoEUsersFromServerAsync(
                2,
                It.Is<IReadOnlyList<string>>(names =>
                    names.Count == 2
                    && names.Contains("user-a")
                    && names.Contains("user-b")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkDeletePppoeUsersResult
            {
                Success = true,
                DeletedCount = 2
            });

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        BulkCopyAccountsToServerResult result = await sut.BulkCopyAccountsToServerAsync(
            10, 5, [1, 2], applyToAllInNetwork: false);

        Assert.True(result.Success);
        Assert.Equal(2, result.AddedCount);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(2, result.ReassignedCount);
        Assert.Equal(2, result.RemovedFromOldCount);
        Assert.Equal(5, (await db.Clients.SingleAsync(c => c.Id == 1)).MikroTikServerId);
        Assert.Equal(5, (await db.Clients.SingleAsync(c => c.Id == 2)).MikroTikServerId);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task BulkCopyAccountsToServerAsync_ApplyToAll_SendsEveryNetworkClient()
    {
        await using ApplicationDbContext db = CreateDb();
        db.MikroTikServers.Add(Server(8, 3));
        db.Clients.Add(MinimalClient(10, "a", 3));
        db.Clients.Add(MinimalClient(11, "b", 3));
        db.Clients.Add(MinimalClient(12, "c", 4));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.AddPPPoEUsersToServerAsync(
                8,
                It.Is<IReadOnlyList<Client>>(list => list.Count == 2),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BulkAddPppoeUsersResult
            {
                Success = true,
                AddedCount = 1,
                SkippedExistingCount = 1,
                PlacedClientIds = [10, 11]
            });

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        BulkCopyAccountsToServerResult result = await sut.BulkCopyAccountsToServerAsync(
            3, 8, null, applyToAllInNetwork: true);

        Assert.True(result.Success);
        Assert.Equal(2, result.RequestedCount);
        Assert.Equal(1, result.AddedCount);
        Assert.Equal(1, result.SkippedExistingCount);
        Assert.Equal(2, result.ReassignedCount);
        Assert.Equal(8, (await db.Clients.SingleAsync(c => c.Id == 10)).MikroTikServerId);
        mikroTik.VerifyAll();
    }

    private static Client MinimalClient(int id, string userName, int networkId, int? serverId = null) =>
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
            ProfileName = "10M",
            IsActive = true,
            MikroTikServerId = serverId
        };

    private static MikroTikServer Server(int id, int networkId) =>
        new()
        {
            Id = id,
            Name = "tower-" + id,
            Host = "10.0.0." + id,
            Port = 8728,
            User = "admin",
            Pass = "secret",
            NetworkId = networkId,
            IsActive = true
        };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
