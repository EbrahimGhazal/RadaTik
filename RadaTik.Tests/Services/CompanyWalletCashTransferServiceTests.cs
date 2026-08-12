using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class CompanyWalletCashTransferServiceTests
{
    [Fact]
    public async Task WithdrawForTopUpApproval_DeductsCashBoxOnly()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new() { Id = 1, Name = "Co", Balance = 100m });
        db.CashBoxes.Add(new()
        {
            Id = 1,
            OwnerType = CashBoxOwnerType.Network,
            OwnerId = 1,
            Balance = 500m
        });
        await db.SaveChangesAsync();

        CompanyWalletCashTransferService service = new(db);
        CompanyWalletCashTransferResult result = await service.WithdrawCompanyCashBoxForTopUpApprovalAsync(
            1, 42, 200m, "admin-1");

        Assert.True(result.Success);
        Assert.NotNull(result.CashBoxWithdrawalId);
        CashBox box = await db.CashBoxes.SingleAsync();
        Assert.Equal(300m, box.Balance);

        Network company = await db.Networks.SingleAsync();
        Assert.Equal(100m, company.Balance);

        CashBoxWithdrawal w = await db.CashBoxWithdrawals
            .IgnoreQueryFilters()
            .SingleAsync(w => w.Id == result.CashBoxWithdrawalId);
        Assert.Equal(42, w.NetworkTopUpRequestId);
    }

    [Fact]
    public async Task WithdrawForTopUpApproval_FailsWhenInsufficient()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new() { Id = 1, Name = "Co", Balance = 0m });
        db.CashBoxes.Add(new()
        {
            OwnerType = CashBoxOwnerType.Network,
            OwnerId = 1,
            Balance = 10m
        });
        await db.SaveChangesAsync();

        CompanyWalletCashTransferService service = new(db);
        CompanyWalletCashTransferResult result = await service.WithdrawCompanyCashBoxForTopUpApprovalAsync(
            1, 1, 100m, "admin-1");

        Assert.False(result.Success);
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
