using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.PricingPolicies;
using RadaTik.Services.Profiles;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ProfileImportPricingServiceTests
{
    [Fact]
    public async Task CalculateProfileChargeAsync_ZeroUnits_HasSufficientBalance()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 1, Name = "Co", Balance = 100m });
        await db.SaveChangesAsync();

        ProfileImportPricingService sut = new(db);
        ProfileImportChargeEstimate estimate = await sut.CalculateProfileChargeAsync(1, 0);

        Assert.True(estimate.HasSufficientBalance);
        Assert.Equal(0m, estimate.TotalCharge);
        Assert.Equal(100m, estimate.WalletBalance);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
