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
    public async Task SyncWithMikroTikAsync_CallsUpdateWhichUpsertsFromDatabase()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(CreateClient(3, profileName: "10M"));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.UpdatePPPoEUser(It.Is<Client>(c =>
                c.Id == 3 && c.Password == "pass" && c.ProfileName == "10M")))
            .ReturnsAsync(true);

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.SyncWithMikroTikAsync(3, 2);

        Assert.True(outcome.IsSuccess);
        Assert.Contains("مزامنة", outcome.SuccessMessage ?? string.Empty);
        mikroTik.Verify(m => m.CheckUserExists(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        mikroTik.Verify(m => m.AddPPPoEUser(It.IsAny<Client>()), Times.Never);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task SyncWithMikroTikAsync_PrefersProfileNameFromLinkedProfile()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Profiles.Add(new Profile { Id = 1, Name = "20M" });
        db.Clients.Add(CreateClient(3, profileName: "stale-name"));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.UpdatePPPoEUser(It.Is<Client>(c => c.ProfileName == "20M")))
            .ReturnsAsync(true);

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.SyncWithMikroTikAsync(3, 2);

        Assert.True(outcome.IsSuccess);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task SyncWithMikroTikAsync_WhenNoMikroTikServer_Fails()
    {
        await using ApplicationDbContext db = CreateDb();
        Client client = CreateClient(3, profileName: "10M");
        client.MikroTikServerId = null;
        db.Clients.Add(client);
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.SyncWithMikroTikAsync(3, 2);

        Assert.False(outcome.IsSuccess);
        Assert.Contains("خادم", outcome.ErrorMessage ?? string.Empty);
        mikroTik.VerifyAll();
    }

    [Fact]
    public async Task SyncWithMikroTikAsync_WhenMikroTikUnreachable_ReturnsFriendlyFailure()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(CreateClient(3, profileName: "10M"));
        await db.SaveChangesAsync();

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.UpdatePPPoEUser(It.IsAny<Client>()))
            .ThrowsAsync(new InvalidOperationException("فشل الاتصال بالخادم 1.2.3.4:8728 بعد 2 محاولات"));

        ClientMikroTikLifecycleService sut = new(db, mikroTik.Object);
        ClientOperationOutcome outcome = await sut.SyncWithMikroTikAsync(3, 2);

        Assert.False(outcome.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(outcome.ErrorMessage));
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

    private static Client CreateClient(int id, string profileName) => new()
    {
        Id = id,
        Name = "sync",
        UserName = "sync-me",
        Password = "pass",
        SID = "1234567890",
        PhoneNumber = "0999999999",
        ProfileId = 1,
        ProfileName = profileName,
        NetworkId = 2,
        MikroTikServerId = 9
    };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
