using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.MaintenancePricing;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class MaintenancePricingSaveTests
{
    [Fact]
    public void BindRows_TreatsCheckedSwitchAsActiveEvenWhenHiddenFalseIsPostedFirst()
    {
        FormCollection form = new(new Dictionary<string, StringValues>
        {
            ["Rows[0].Type"] = ((int)MaintenanceType.CableReplacement).ToString(),
            ["Rows[0].AmountSyp"] = "1,250.50",
            ["Rows[0].IsActive"] = new StringValues(["false", "true"]),
            ["Rows[1].Type"] = nameof(MaintenanceType.ReceiverReplacement),
            ["Rows[1].AmountSyp"] = "0",
            ["Rows[1].IsActive"] = "false"
        });

        List<MaintenancePricingBulkSaveRowInput> rows = MaintenancePricingBulkSaveInput.BindRows(form);

        Assert.Equal(2, rows.Count);
        Assert.Equal(MaintenanceType.CableReplacement, rows[0].Type);
        Assert.Equal(1250.50m, rows[0].AmountSyp);
        Assert.True(rows[0].IsActive);
        Assert.Equal(MaintenanceType.ReceiverReplacement, rows[1].Type);
        Assert.False(rows[1].IsActive);
    }

    [Fact]
    public async Task SaveRowsAsync_UpdatesExistingPriceAndActiveFlag()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 4, Name = "شركة" });
        db.NetworkMaintenancePrices.Add(new NetworkMaintenancePrice
        {
            NetworkId = 4,
            MaintenanceType = MaintenanceType.CableReplacement,
            AmountSYP = 10m,
            IsActive = false,
            UpdatedByUserId = "old"
        });
        await db.SaveChangesAsync();

        MaintenancePricingService sut = new(db, new IMaintenancePricingScopeStrategy[]
        {
            new TestMainScope(),
            new TestCurrentScope()
        });

        MaintenancePricingOperationResult result = await sut.SaveRowsAsync(
            4,
            "main",
            [
                new MaintenancePricingBulkSaveRowInput
                {
                    Type = MaintenanceType.CableReplacement,
                    AmountSyp = 80m,
                    IsActive = true
                }
            ],
            "actor-1");

        Assert.True(result.Success);
        NetworkMaintenancePrice saved = Assert.Single(await db.NetworkMaintenancePrices
            .Where(p => p.NetworkId == 4 && p.MaintenanceType == MaintenanceType.CableReplacement)
            .ToListAsync());
        Assert.Equal(80m, saved.AmountSYP);
        Assert.True(saved.IsActive);
        Assert.Equal("actor-1", saved.UpdatedByUserId);
    }

    private sealed class TestMainScope : IMaintenancePricingScopeStrategy
    {
        public string ScopeKey => "main";
        public bool IsAvailable(MaintenancePricingScopeContext context) => true;
        public int ResolveTargetNetworkId(MaintenancePricingScopeContext context) => context.MainNetworkId;
    }

    private sealed class TestCurrentScope : IMaintenancePricingScopeStrategy
    {
        public string ScopeKey => "current";
        public bool IsAvailable(MaintenancePricingScopeContext context) => context.CanUseCurrentScope;
        public int ResolveTargetNetworkId(MaintenancePricingScopeContext context) => context.CurrentNetworkId;
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
