using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Helpers;
using RadaTik.Services;
using RadaTik.Services.CollectionPoint;
using Xunit;

namespace RadaTik.Tests.Services;

public class CollectionPointReceivePaymentServiceTests
{
    [Fact]
    public async Task ProcessAsync_ReturnsNotFound_WhenClientMissing()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using ApplicationDbContext db = new(options);
        Mock<ICollectionPaymentService> payment = new();
        Mock<ICollectionCommissionChargeService> commission = new();

        CollectionPointReceivePaymentService service = new(
            db,
            payment.Object,
            commission.Object,
            new CurrencyHelperAdapter());

        ReceivePaymentOutcome outcome = await service.ProcessAsync(
            new ReceivePaymentCommand(99, 1000m, null, null, "user-1", 1));

        Assert.True(outcome.NotFound);
    }
}
