using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientRenewalGuardServiceTests
{
    [Fact]
    public async Task CheckBlockingInvoicesAsync_NoPendingInvoices_ReturnsCanRenewTrue()
    {
        await using var db = CreateDb();
        var sut = new ClientRenewalGuardService(db);

        var result = await sut.CheckBlockingInvoicesAsync(clientId: 11);

        Assert.True(result.CanRenew);
        Assert.Equal(0, result.PendingInvoicesCount);
        Assert.Equal(0m, result.TotalOutstanding);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public async Task CheckBlockingInvoicesAsync_PendingInvoices_ReturnsBlockedWithTotals()
    {
        await using var db = CreateDb();
        SeedPendingInvoices(db);

        var sut = new ClientRenewalGuardService(db);
        var result = await sut.CheckBlockingInvoicesAsync(clientId: 11);

        Assert.False(result.CanRenew);
        Assert.Equal(2, result.PendingInvoicesCount);
        Assert.Equal(450m, result.TotalOutstanding);
        Assert.Contains(result.Reasons, r => r.Contains("#1001"));
        Assert.Contains(result.Reasons, r => r.Contains("#1002"));
    }

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }

    private static void SeedPendingInvoices(ApplicationDbContext db)
    {
        db.MaintenanceInvoices.AddRange(
            new MaintenanceInvoice
            {
                Id = 1001,
                MaintenanceRequestId = 1,
                ClientId = 11,
                NetworkId = 1,
                IssuedByUserId = "tech-1",
                FaultExplanation = "fault",
                FixExplanation = "fix",
                ServiceBasePrice = 300m,
                TransportFee = 0m,
                GrossAmount = 300m,
                CommissionMode = MaintenanceCommissionMode.Fixed,
                CommissionValue = 30m,
                CommissionAmount = 30m,
                NetAmountToCompany = 270m,
                Status = MaintenanceInvoiceStatus.Pending,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new MaintenanceInvoice
            {
                Id = 1002,
                MaintenanceRequestId = 2,
                ClientId = 11,
                NetworkId = 1,
                IssuedByUserId = "tech-1",
                FaultExplanation = "fault",
                FixExplanation = "fix",
                ServiceBasePrice = 150m,
                TransportFee = 0m,
                GrossAmount = 150m,
                CommissionMode = MaintenanceCommissionMode.Fixed,
                CommissionValue = 15m,
                CommissionAmount = 15m,
                NetAmountToCompany = 135m,
                Status = MaintenanceInvoiceStatus.Pending,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            },
            new MaintenanceInvoice
            {
                Id = 1003,
                MaintenanceRequestId = 3,
                ClientId = 11,
                NetworkId = 1,
                IssuedByUserId = "tech-1",
                FaultExplanation = "fault",
                FixExplanation = "fix",
                ServiceBasePrice = 99m,
                TransportFee = 0m,
                GrossAmount = 99m,
                CommissionMode = MaintenanceCommissionMode.Fixed,
                CommissionValue = 9m,
                CommissionAmount = 9m,
                NetAmountToCompany = 90m,
                Status = MaintenanceInvoiceStatus.Paid,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            });
        db.SaveChanges();
    }
}
