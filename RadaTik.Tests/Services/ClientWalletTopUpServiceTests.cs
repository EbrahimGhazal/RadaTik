using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientWalletTopUpServiceTests
{
    [Fact]
    public async Task TopUpAsync_SystemAdmin_IncreasesClientBalance()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(new Client
        {
            Id = 1,
            Name = "c1",
            UserName = "c1",
            Password = "p",
            SID = "1234567890",
            PhoneNumber = "0",
            ProfileId = 1,
            NetworkId = 2,
            Balance = 100m
        });
        await db.SaveChangesAsync();

        Mock<IRequestNotificationService> notifications = new();
        notifications
            .Setup(n => n.NotifyClientTopUpSubmittedAsync(1, 2, 50m, "مدير النظام", It.IsAny<string?>()))
            .Returns(Task.CompletedTask);

        ClientWalletTopUpService sut = new(db, notifications.Object);
        ClientWalletTopUpOutcome outcome = await sut.TopUpAsync(new ClientWalletTopUpCommand
        {
            ClientId = 1,
            Amount = 50m,
            ActorUserId = "admin-1",
            SourceType = ClientTopUpSource.SystemAdmin,
            ActorDisplayName = "Admin"
        });

        Assert.True(outcome.IsSuccess);
        Assert.Equal(150m, (await db.Clients.FindAsync(1))!.Balance);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
