using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class MaintenanceBillingServiceTests
{
    [Fact]
    public async Task PayInvoiceFromClientWalletAsync_DeductsClientAndCreditsCompany()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "Co", Balance = 0m });
        db.Clients.Add(CreateClient(10, 500m));
        db.MaintenanceInvoices.Add(new MaintenanceInvoice
        {
            Id = 1,
            MaintenanceRequestId = 1,
            ClientId = 10,
            NetworkId = 1,
            IssuedByUserId = "issuer",
            FaultExplanation = "عطل",
            FixExplanation = "إصلاح",
            GrossAmount = 100m,
            NetAmountToCompany = 80m,
            ServiceBasePrice = 90m,
            TransportFee = 10m,
            CommissionMode = MaintenanceCommissionMode.Percent,
            CommissionValue = 10m,
            CommissionAmount = 10m,
            Status = MaintenanceInvoiceStatus.Pending
        });
        await db.SaveChangesAsync();

        MaintenanceBillingService service = CreateService(db);

        MaintenanceInvoicePaymentResult result = await service.PayInvoiceFromClientWalletAsync(1, "payer-1");

        Assert.True(result.Success);
        Assert.Equal(400m, (await db.Clients.SingleAsync()).Balance);
        Assert.Equal(80m, (await db.Networks.SingleAsync()).Balance);
        MaintenanceInvoice invoice = await db.MaintenanceInvoices.SingleAsync();
        Assert.Equal(MaintenanceInvoiceStatus.Paid, invoice.Status);
        Assert.Single(await db.PaymentTransactions.ToListAsync());
        Assert.Single(await db.NetworkWalletTransactions.ToListAsync());
    }

    [Fact]
    public async Task PayInvoiceFromClientWalletAsync_ReturnsInsufficientBalance_WhenClientShort()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "Co", Balance = 0m });
        db.Clients.Add(CreateClient(10, 50m));
        db.MaintenanceInvoices.Add(new MaintenanceInvoice
        {
            Id = 2,
            MaintenanceRequestId = 1,
            ClientId = 10,
            NetworkId = 1,
            IssuedByUserId = "issuer",
            FaultExplanation = "عطل",
            FixExplanation = "إصلاح",
            GrossAmount = 100m,
            NetAmountToCompany = 80m,
            Status = MaintenanceInvoiceStatus.Pending
        });
        await db.SaveChangesAsync();

        MaintenanceBillingService service = CreateService(db);

        MaintenanceInvoicePaymentResult result = await service.PayInvoiceFromClientWalletAsync(2, "payer-1");

        Assert.False(result.Success);
        Assert.True(result.InsufficientBalance);
        Assert.Equal(100m, result.RequiredAmount);
    }

    [Fact]
    public async Task PayInvoiceFromClientWalletAsync_RejectsNonPendingInvoice()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "Co", Balance = 0m });
        db.Clients.Add(CreateClient(10, 500m));
        db.MaintenanceInvoices.Add(new MaintenanceInvoice
        {
            Id = 3,
            MaintenanceRequestId = 1,
            ClientId = 10,
            NetworkId = 1,
            IssuedByUserId = "issuer",
            FaultExplanation = "عطل",
            FixExplanation = "إصلاح",
            GrossAmount = 100m,
            NetAmountToCompany = 80m,
            Status = MaintenanceInvoiceStatus.Paid
        });
        await db.SaveChangesAsync();

        MaintenanceBillingService service = CreateService(db);

        MaintenanceInvoicePaymentResult result = await service.PayInvoiceFromClientWalletAsync(3, "payer-1");

        Assert.False(result.Success);
        Assert.Contains("بانتظار", result.ErrorMessage ?? string.Empty);
    }

    private static Client CreateClient(int id, decimal balance) => new()
    {
        Id = id,
        Name = "Test",
        UserName = "u1",
        Password = "p",
        SID = "sid",
        PhoneNumber = "099",
        Balance = balance,
        NetworkId = 1,
        ProfileId = 1
    };

    private static MaintenanceBillingService CreateService(ApplicationDbContext db) =>
        new(db, new RequestNotificationService(db, NullLogger<RequestNotificationService>.Instance), NullLogger<MaintenanceBillingService>.Instance);

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options);
    }
}
