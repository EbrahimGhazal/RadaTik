using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientWalletTopUpApprovalServiceTests
{
    [Fact]
    public async Task ApproveAsync_CreditsClientAndMarksApproved()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(CreateTestClient(10, 100m));
        db.PaymentMethods.Add(new PaymentMethod { Id = 1, Name = "Cash", IsActive = true });
        db.ClientWalletTopUpRequests.Add(new ClientWalletTopUpRequest
        {
            Id = 1,
            ClientId = 10,
            NetworkId = 1,
            Amount = 50m,
            PaymentMethodId = 1,
            Status = ClientWalletTopUpRequestStatus.Pending,
            RecipientTarget = ClientWalletTopUpRecipientTarget.CompanyManager,
            RequestedByUserId = "user-1",
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        ClientWalletTopUpApprovalService service = new(db);
        ClientWalletTopUpApprovalResult result = await service.ApproveAsync(
            1, "admin-1", ClientWalletTopUpRecipientTarget.CompanyManager, null);

        Assert.True(result.Success);
        Client client = await db.Clients.SingleAsync();
        Assert.Equal(150m, client.Balance);

        ClientWalletTopUpRequest req = await db.ClientWalletTopUpRequests.SingleAsync();
        Assert.Equal(ClientWalletTopUpRequestStatus.Approved, req.Status);

        ClientTopUpTransaction tx = await db.ClientTopUpTransactions.SingleAsync();
        Assert.Equal(50m, tx.Amount);
        Assert.Equal(ClientTopUpSource.ClientPortalRequest, tx.SourceType);
    }

    [Fact]
    public async Task RejectAsync_DoesNotChangeBalance()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(CreateTestClient(10, 100m));
        db.PaymentMethods.Add(new PaymentMethod { Id = 1, Name = "Cash", IsActive = true });
        db.ClientWalletTopUpRequests.Add(new ClientWalletTopUpRequest
        {
            Id = 2,
            ClientId = 10,
            NetworkId = 1,
            Amount = 50m,
            PaymentMethodId = 1,
            Status = ClientWalletTopUpRequestStatus.Pending,
            RecipientTarget = ClientWalletTopUpRecipientTarget.CompanyManager,
            RequestedByUserId = "user-1",
            RequestedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        ClientWalletTopUpApprovalService service = new(db);
        ClientWalletTopUpApprovalResult result = await service.RejectAsync(
            2, "admin-1", ClientWalletTopUpRecipientTarget.CompanyManager, null, "مرفوض");

        Assert.True(result.Success);
        Assert.Equal(100m, (await db.Clients.SingleAsync()).Balance);
        Assert.Equal(ClientWalletTopUpRequestStatus.Rejected, (await db.ClientWalletTopUpRequests.SingleAsync()).Status);
    }

    private static Client CreateTestClient(int id, decimal balance) => new()
    {
        Id = id,
        Name = "Test",
        UserName = "u1",
        Password = "p",
        SID = "sid-1",
        PhoneNumber = "099",
        Balance = balance,
        NetworkId = 1,
        ProfileId = 1
    };

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
