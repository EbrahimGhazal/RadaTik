using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class SubscriberFaultDiagnosisServiceTests
{
    [Fact]
    public async Task DiagnoseAsync_UnknownClient_ReturnsNotFound()
    {
        await using ApplicationDbContext db = CreateDb();
        SubscriberFaultDiagnosisService sut = CreateSut(db, new Mock<IMikroTikPppoeUserService>(), new Mock<IMikroTikProbeService>());

        SubscriberFaultDiagnosisDto dto = await sut.DiagnoseAsync(clientId: 99, selectedNetworkId: 1);

        Assert.False(dto.Success);
        Assert.Equal("NotFound", dto.Status);
    }

    [Fact]
    public async Task DiagnoseAsync_ClientOutsideCompany_ReturnsForbidden()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedTopology(db, clientNetworkId: 2);
        SubscriberFaultDiagnosisService sut = CreateSut(db, new Mock<IMikroTikPppoeUserService>(), new Mock<IMikroTikProbeService>());

        SubscriberFaultDiagnosisDto dto = await sut.DiagnoseAsync(clientId: 1, selectedNetworkId: 1);

        Assert.False(dto.Success);
        Assert.Equal("Forbidden", dto.Status);
    }

    [Fact]
    public async Task DiagnoseAsync_AllReceiverPeersDown_ReturnsReceiver()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedTopology(db, clientNetworkId: 1, extraReceiverClients: 2);
        Mock<IMikroTikPppoeUserService> pppoe = new();
        pppoe.Setup(s => s.GetActivePPPoEUsers(10)).ReturnsAsync(
        [
            new Client { UserName = "healthy-peer" }
        ]);
        Mock<IMikroTikProbeService> probe = new();
        probe.Setup(s => s.PingManyAsync(10, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, MikroTikPingHopResult>(StringComparer.OrdinalIgnoreCase)
            {
                ["10.0.0.2"] = new() { Address = "10.0.0.2", Attempted = true, Reached = true },
                ["10.0.0.10"] = new() { Address = "10.0.0.10", Attempted = true, Reached = false }
            });

        SubscriberFaultDiagnosisService sut = CreateSut(db, pppoe, probe);

        SubscriberFaultDiagnosisDto dto = await sut.DiagnoseAsync(clientId: 1, selectedNetworkId: 1);

        Assert.True(dto.Success);
        Assert.Equal("Receiver", dto.Cause);
        Assert.Equal("High", dto.Confidence);
    }

    [Fact]
    public async Task DiagnoseAsync_PppSessionUp_ReturnsRouter()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedTopology(db, clientNetworkId: 1, extraReceiverClients: 1);
        Mock<IMikroTikPppoeUserService> pppoe = new();
        pppoe.Setup(s => s.GetActivePPPoEUsers(10)).ReturnsAsync(
        [
            new Client { UserName = "user-1" }
        ]);
        Mock<IMikroTikProbeService> probe = new();
        probe.Setup(s => s.PingManyAsync(10, It.IsAny<IReadOnlyList<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, MikroTikPingHopResult>());

        SubscriberFaultDiagnosisService sut = CreateSut(db, pppoe, probe);

        SubscriberFaultDiagnosisDto dto = await sut.DiagnoseAsync(clientId: 1, selectedNetworkId: 1);

        Assert.True(dto.Success);
        Assert.Equal("Router", dto.Cause);
    }

    [Fact]
    public async Task DiagnoseAsync_ApiThrows_ReturnsServer()
    {
        await using ApplicationDbContext db = CreateDb();
        SeedTopology(db, clientNetworkId: 1);
        Mock<IMikroTikPppoeUserService> pppoe = new();
        pppoe.Setup(s => s.GetActivePPPoEUsers(10)).ThrowsAsync(new InvalidOperationException("down"));
        SubscriberFaultDiagnosisService sut = CreateSut(db, pppoe, new Mock<IMikroTikProbeService>());

        SubscriberFaultDiagnosisDto dto = await sut.DiagnoseAsync(clientId: 1, selectedNetworkId: 1);

        Assert.True(dto.Success);
        Assert.Equal("Server", dto.Cause);
    }

    private static SubscriberFaultDiagnosisService CreateSut(
        ApplicationDbContext db,
        Mock<IMikroTikPppoeUserService> pppoe,
        Mock<IMikroTikProbeService> probe) =>
        new(db, pppoe.Object, probe.Object, NullLogger<SubscriberFaultDiagnosisService>.Instance);

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        ApplicationDbContext db = new(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static void SeedTopology(ApplicationDbContext db, int clientNetworkId, int extraReceiverClients = 0)
    {
        db.MikroTikServers.Add(new MikroTikServer
        {
            Id = 10,
            Name = "tower-a",
            Host = "10.0.0.1",
            Port = 8728,
            User = "admin",
            Pass = "pass",
            NetworkId = 1,
            IsActive = true
        });
        db.Sectors.Add(new Sector
        {
            Id = 20,
            Name = "sector-a",
            Latitude = 33,
            Longitude = 36,
            Direction = 90,
            CoverageAngle = 90,
            CoverageRange = 5,
            IPAddress = "10.0.0.2",
            NetworkMask = "255.255.255.0",
            MikroTikServerId = 10,
            NetworkId = 1
        });
        db.Receivers.Add(new Receiver
        {
            Id = 30,
            Name = "cpe-a",
            Latitude = 33.1,
            Longitude = 36.1,
            IPAddress = "10.0.0.10",
            NetworkMask = "255.255.255.0",
            SectorId = 20,
            NetworkId = 1
        });
        db.Receivers.Add(new Receiver
        {
            Id = 31,
            Name = "cpe-b",
            Latitude = 33.2,
            Longitude = 36.2,
            IPAddress = "10.0.0.11",
            NetworkMask = "255.255.255.0",
            SectorId = 20,
            NetworkId = 1
        });
        db.Clients.Add(CreateClient(1, "user-1", clientNetworkId, receiverId: 30));
        for (int i = 0; i < extraReceiverClients; i++)
        {
            int id = 2 + i;
            db.Clients.Add(CreateClient(id, "user-" + id, clientNetworkId, receiverId: 30));
        }

        db.Clients.Add(CreateClient(80, "healthy-peer", clientNetworkId, receiverId: 31));

        db.SaveChanges();
    }

    private static Client CreateClient(int id, string userName, int networkId, int receiverId) => new()
    {
        Id = id,
        Name = "مشترك " + id,
        SID = "100000000" + id,
        UserName = userName,
        Password = "pwd",
        PhoneNumber = "099000000" + id,
        ProfileId = 1,
        NetworkId = networkId,
        ReceiverId = receiverId,
        MikroTikServerId = 10,
        IsActive = true,
        AccountExpirationDate = DateTime.Now.AddDays(20)
    };
}
