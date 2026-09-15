using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientLiveConnectionRefreshServiceTests
{
    [Fact]
    public async Task RefreshAll_SkipsInactiveServersAndMatchesConnectedClients()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 10, Name = "Company" });
        db.MikroTikServers.AddRange(
            Server(1, 10, active: true),
            Server(2, 10, active: false));
        db.Clients.AddRange(
            Client(11, 10, 1, "online-user"),
            Client(12, 10, 1, "offline-user"));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(s => s.QueryActivePppSessionNamesByServerAsync(
                It.Is<IReadOnlyCollection<int>>(ids => ids.Contains(1) && !ids.Contains(2)),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PppActiveSessionQueryResult(1, ["online-user"], Succeeded: true)
            ]);

        ClientLiveConnectionStore store = new();
        ClientLiveConnectionRefreshService sut = new(
            db,
            mikroTik.Object,
            store,
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<ClientLiveConnectionRefreshService>.Instance);

        await sut.RefreshAllActiveServersAsync();

        Assert.True(store.TryGetCompanySnapshot(10, out CompanyLiveConnectionSnapshot? snapshot));
        Assert.True(snapshot!.Ready);
        Assert.Single(snapshot.ConnectedClientIds);
        Assert.Contains(11, snapshot.ConnectedClientIds);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task RefreshAll_KeepsPreviousSessionsWhenRouterFails()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 10, Name = "Company" });
        db.MikroTikServers.Add(Server(1, 10, active: true));
        db.Clients.Add(Client(11, 10, 1, "online-user"));
        await db.SaveChangesAsync();

        ClientLiveConnectionStore store = new();
        store.SetServerResult(1, ["online-user"], succeeded: true);

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(s => s.QueryActivePppSessionNamesByServerAsync(
                It.IsAny<IReadOnlyCollection<int>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new PppActiveSessionQueryResult(1, [], Succeeded: false)
            ]);

        ClientLiveConnectionRefreshService sut = new(
            db,
            mikroTik.Object,
            store,
            Mock.Of<IServiceScopeFactory>(),
            NullLogger<ClientLiveConnectionRefreshService>.Instance);

        await sut.RefreshAllActiveServersAsync();

        Assert.True(store.TryGetCompanySnapshot(10, out CompanyLiveConnectionSnapshot? snapshot));
        Assert.Contains(11, snapshot!.ConnectedClientIds);
    }

    private static MikroTikServer Server(int id, int networkId, bool active) => new()
    {
        Id = id,
        Name = $"s{id}",
        Host = $"10.0.0.{id}",
        Port = 8728,
        User = "api",
        Pass = "secret",
        NetworkId = networkId,
        IsActive = active
    };

    private static Client Client(int id, int networkId, int serverId, string userName) => new()
    {
        Id = id,
        Name = userName,
        UserName = userName,
        Password = "p",
        SID = id.ToString(),
        PhoneNumber = "0",
        ProfileId = 1,
        NetworkId = networkId,
        MikroTikServerId = serverId,
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
