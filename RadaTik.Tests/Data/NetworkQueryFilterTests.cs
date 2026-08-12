using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using Xunit;

namespace RadaTik.Tests.Data;

public class NetworkQueryFilterTests
{
    [Fact]
    public async Task Filter_disabled_by_default_shows_all_clients()
    {
        await using var db = CreateDb();
        db.Clients.Add(CreateClient("A", "a", 1));
        db.Clients.Add(CreateClient("B", "b", 2));
        await db.SaveChangesAsync();

        int count = await db.Clients.CountAsync();
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task Filter_enabled_restricts_to_accessible_networks()
    {
        await using var db = CreateDb();
        db.Clients.Add(CreateClient("A", "a", 1));
        db.Clients.Add(CreateClient("B", "b", 2));
        await db.SaveChangesAsync();

        var scope = new CurrentNetworkScope();
        scope.SetScope(isFilterActive: true, bypassAllNetworks: false, [1]);
        db.ApplyNetworkScope(scope);

        var names = await db.Clients.Select(c => c.Name).ToListAsync();
        Assert.Single(names);
        Assert.Equal("A", names[0]);
    }

    [Fact]
    public async Task IgnoreQueryFilters_bypasses_tenant_scope()
    {
        await using var db = CreateDb();
        db.Clients.Add(CreateClient("A", "a", 1));
        db.Clients.Add(CreateClient("B", "b", 2));
        await db.SaveChangesAsync();

        var scope = new CurrentNetworkScope();
        scope.SetScope(isFilterActive: true, bypassAllNetworks: false, [1]);
        db.ApplyNetworkScope(scope);

        int count = await db.Clients.IgnoreQueryFilters().CountAsync();
        Assert.Equal(2, count);
    }

    private static Client CreateClient(string name, string userName, int networkId) => new()
    {
        Name = name,
        UserName = userName,
        Password = "pwd",
        SID = "1234567890",
        PhoneNumber = "0999999999",
        ProfileId = 1,
        NetworkId = networkId
    };

    private static ApplicationDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new ApplicationDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }
}
