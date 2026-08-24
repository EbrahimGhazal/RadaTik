using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.MikroTik;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientSelfRenewalServiceTests
{
    [Fact]
    public async Task RenewFromWalletAsync_SufficientBalance_RenewsAndCharges()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Profiles.Add(new Profile
        {
            Id = 1,
            Name = "10M",
            Price = 100m,
            VATPercentage = 0m,
            NetworkId = 2,
            MikroTikServerId = 3
        });
        db.Clients.Add(new Client
        {
            Id = 5,
            Name = "self",
            UserName = "self",
            Password = "p",
            SID = "1234567890",
            PhoneNumber = "0",
            ProfileId = 1,
            NetworkId = 2,
            Balance = 1000m,
            MikroTikServerId = 3
        });
        await db.SaveChangesAsync();

        Mock<IClientRenewalGuardService> guard = new();
        guard.Setup(g => g.CheckBlockingInvoicesAsync(5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RenewalBlockResult { CanRenew = true });

        Mock<IMikroTikPppoeUserService> mikroTik = new(MockBehavior.Strict);
        mikroTik
            .Setup(m => m.RenewPPPoESubscription("self", 3, It.IsAny<DateTime>()))
            .ReturnsAsync(true);

        ClientSelfRenewalService sut = new(db, guard.Object, mikroTik.Object, new ClientVipPolicyService(db));
        ClientOperationOutcome outcome = await sut.RenewFromWalletAsync(5);

        Assert.True(outcome.IsSuccess);
        Client updated = (await db.Clients.Include(c => c.Profile).FirstAsync(c => c.Id == 5))!;
        Assert.Equal(900m, updated.Balance);
        mikroTik.VerifyAll();
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
