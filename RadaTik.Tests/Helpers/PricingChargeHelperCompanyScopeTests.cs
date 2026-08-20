using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Helpers;

public sealed class PricingChargeHelperCompanyScopeTests
{
    [Fact]
    public async Task GetCompanyScopeNetworkIdsForSelectedAsync_IncludesParentAndChildren()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.AddRange(
            new Network { Id = 10, Name = "Company" },
            new Network { Id = 11, Name = "Tower A", ParentNetworkId = 10 },
            new Network { Id = 12, Name = "Tower B", ParentNetworkId = 10 },
            new Network { Id = 99, Name = "Other Co" });
        await db.SaveChangesAsync();

        List<int> fromParent = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(db, 10);
        List<int> fromChild = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(db, 11);

        Assert.Equal([10, 11, 12], fromParent.OrderBy(id => id).ToArray());
        Assert.Equal(fromParent.OrderBy(id => id).ToArray(), fromChild.OrderBy(id => id).ToArray());
        Assert.Equal(10, fromChild[0]);
        Assert.DoesNotContain(99, fromParent);
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
