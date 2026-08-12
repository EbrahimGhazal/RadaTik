using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientMikroTikLifecycleServiceSyncTests
{
    [Fact]
    public async Task SyncWithMikroTikAsync_UpdatesUserOnRouter()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(new Client
        {
            Id = 3,
            Name = "sync-me",
            UserName = "sync-me",
            Password = "pass",
            SID = "1234567890",
            PhoneNumber = "0999999999",
            ProfileId = 1,
            NetworkId = 2,
            MikroTikServerId = 9
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.UpdatePPPoEUser(It.Is<Client>(c => c.Id == 3)))
            .ReturnsAsync(true);

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.SyncWithMikroTikAsync(3, 2);

        Assert.True(outcome.IsSuccess);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task QuickExtendAsync_AddsDaysAndCallsMikroTik()
    {
        await using ApplicationDbContext db = CreateDb();
        DateTime current = DateTime.Now.AddDays(5);
        db.Clients.Add(new Client
        {
            Id = 4,
            Name = "extend",
            UserName = "extend",
            Password = "pass",
            SID = "1234567890",
            PhoneNumber = "0999999999",
            ProfileId = 1,
            NetworkId = 2,
            MikroTikServerId = 9,
            AccountExpirationDate = current
        });
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.RenewPPPoESubscription("extend", 9, It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.QuickExtendAsync(4, 2, 7);

        Assert.True(outcome.IsSuccess);
        Client updated = await db.Clients.SingleAsync(c => c.Id == 4);
        Assert.Equal(current.AddDays(7).Date, updated.AccountExpirationDate!.Value.Date);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
