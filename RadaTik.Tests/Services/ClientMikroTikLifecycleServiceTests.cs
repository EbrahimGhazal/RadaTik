using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientMikroTikLifecycleServiceTests
{
    [Fact]
    public async Task ToggleActiveAsync_FlipsIsActive()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(1, "u1", 10));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);

        ClientOperationOutcome outcome = await sut.ToggleActiveAsync(1, 10);

        Assert.True(outcome.IsSuccess);
        Client updated = await db.Clients.SingleAsync(c => c.Id == 1);
        Assert.False(updated.IsActive);
    }

    [Fact]
    public async Task FreezeAsync_NoServer_ReturnsFail()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(2, "u2", 10));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);

        ClientOperationOutcome outcome = await sut.FreezeAsync(2, 10);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("المايكروتك", outcome.ErrorMessage);
        mikroTik.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task FreezeAsync_WithServer_CallsMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(MinimalClient(3, "ppp-user", 10, 5));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.FreezeAccount(5, "ppp-user"))
            .ReturnsAsync(true);

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.FreezeAsync(3, 10);

        Assert.True(outcome.IsSuccess);
        mikroTik.Verify(m => m.FreezeAccount(5, "ppp-user"), Times.Once);
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
            IsActive = true,
            MikroTikServerId = serverId
        };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
