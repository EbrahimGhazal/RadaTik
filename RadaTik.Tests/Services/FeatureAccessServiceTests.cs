using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using System.Security.Claims;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class FeatureAccessServiceTests
{
    [Fact]
    public async Task HasFeatureAsync_ReturnsTrue_ForNetworkAdministrator_WithoutSubscription()
    {
        await using ApplicationDbContext db = CreateDb();
        (ApplicationUser user, ClaimsPrincipal principal, DefaultHttpContext httpContext) = await SeedNetworkAdminAsync(db);

        FeatureAccessService service = new FeatureAccessService(db, CreateUserManagerMock(user).Object);
        bool allowed = await service.HasFeatureAsync(principal, httpContext, FeatureKeys.Clients);

        Assert.True(allowed);
    }

    [Fact]
    public async Task HasFeatureAsync_ReturnsTrue_WhenActiveSubscriptionExists()
    {
        await using ApplicationDbContext db = CreateDb();
        (ApplicationUser user, ClaimsPrincipal principal, DefaultHttpContext httpContext) = await SeedNetworkAdminAsync(db);
        DateTime now = DateTime.Now;

        db.NetworkServiceSubscriptions.Add(new NetworkServiceSubscription
        {
            NetworkId = user.NetworkId!.Value,
            FeatureKey = FeatureKeys.Clients,
            BillingPeriod = PricingBillingPeriod.Monthly,
            StartAt = now,
            ExpiresAt = now.AddMonths(1),
            Status = NetworkServiceSubscriptionStatus.Active
        });
        await db.SaveChangesAsync();

        FeatureAccessService service = new FeatureAccessService(db, CreateUserManagerMock(user).Object);
        bool allowed = await service.HasFeatureAsync(principal, httpContext, FeatureKeys.Clients);

        Assert.True(allowed);
    }

    [Fact]
    public async Task HasFeatureAsync_ReturnsTrue_ForLegacyNetworkFeature()
    {
        await using ApplicationDbContext db = CreateDb();
        (ApplicationUser user, ClaimsPrincipal principal, DefaultHttpContext httpContext) = await SeedNetworkAdminAsync(db);

        db.NetworkFeatures.Add(new NetworkFeature
        {
            NetworkId = user.NetworkId!.Value,
            Key = FeatureKeys.Reports,
            IsEnabled = true
        });
        await db.SaveChangesAsync();

        FeatureAccessService service = new FeatureAccessService(db, CreateUserManagerMock(user).Object);
        bool allowed = await service.HasFeatureAsync(principal, httpContext, FeatureKeys.Reports);

        Assert.True(allowed);
    }

    [Fact]
    public async Task HasFeatureAsync_ReturnsTrue_ForSystemAdministrator()
    {
        await using ApplicationDbContext db = CreateDb();
        ApplicationUser admin = new ApplicationUser { Id = "sys-1", UserName = "sys@test", Email = "sys@test" };
        db.Users.Add(admin);
        await db.SaveChangesAsync();

        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, admin.Id),
            new Claim(ClaimTypes.Role, RoleNames.SystemAdministrator)
        ], "test"));

        DefaultHttpContext httpContext = new DefaultHttpContext();
        FeatureAccessService service = new FeatureAccessService(db, CreateUserManagerMock(admin).Object);
        bool allowed = await service.HasFeatureAsync(principal, httpContext, FeatureKeys.Clients);

        Assert.True(allowed);
    }

    private static async Task<(ApplicationUser User, ClaimsPrincipal Principal, DefaultHttpContext HttpContext)> SeedNetworkAdminAsync(
        ApplicationDbContext db)
    {
        Network network = new Network { Id = 10, Name = "Main Co", Balance = 5000m };
        db.Networks.Add(network);

        ApplicationUser user = new ApplicationUser
        {
            Id = "mgr-1",
            UserName = "mgr@test",
            Email = "mgr@test",
            NetworkId = network.Id
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        ClaimsPrincipal principal = new ClaimsPrincipal(new ClaimsIdentity([
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Role, RoleNames.NetworkAdministrator)
        ], "test"));

        DefaultHttpContext httpContext = new DefaultHttpContext { User = principal };
        httpContext.Session = new TestSession();
        httpContext.Session.SetInt32("SelectedNetworkId", network.Id);

        return (user, principal, httpContext);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(ApplicationUser user)
    {
        Mock<IUserStore<ApplicationUser>> store = new Mock<IUserStore<ApplicationUser>>();
        Mock<UserManager<ApplicationUser>> mock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mock.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        mock.Setup(x => x.GetUserId(It.IsAny<ClaimsPrincipal>())).Returns(user.Id);
        return mock;
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private sealed class TestSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();

        public IEnumerable<string> Keys => _store.Keys;
        public string Id => Guid.NewGuid().ToString("N");
        public bool IsAvailable => true;

        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
