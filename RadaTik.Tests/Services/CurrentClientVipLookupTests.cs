using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class CurrentClientVipLookupTests
{
    [Fact]
    public async Task ResolveAsync_ReturnsVipFlagForSignedInClient()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(new Client
        {
            Id = 11,
            Name = "مشترك مميز",
            SID = "1",
            UserName = "vip-user",
            Password = "x",
            ProfileId = 1,
            PhoneNumber = "0900000000",
            IsVip = true,
            VipNote = "عميل قديم"
        });
        await db.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = CreateUserManager(new ApplicationUser
        {
            Id = "u-vip",
            UserName = "vip-user",
            ClientId = 11
        });

        (bool isVip, string? note) = await CurrentClientVipLookup.ResolveAsync(
            userManager.Object,
            db,
            Principal(RoleNames.Client));

        Assert.True(isVip);
        Assert.Equal("عميل قديم", note);
    }

    [Fact]
    public async Task ResolveAsync_IgnoresNonClientRoles()
    {
        await using ApplicationDbContext db = CreateDb();
        Mock<UserManager<ApplicationUser>> userManager = CreateUserManager(new ApplicationUser
        {
            Id = "u-admin",
            ClientId = 11
        });

        (bool isVip, string? note) = await CurrentClientVipLookup.ResolveAsync(
            userManager.Object,
            db,
            Principal(RoleNames.NetworkAdministrator));

        Assert.False(isVip);
        Assert.Null(note);
        userManager.Verify(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_ReturnsFalseWhenClientIsNotVip()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Clients.Add(new Client
        {
            Id = 12,
            Name = "عادي",
            SID = "2",
            UserName = "normal",
            Password = "x",
            ProfileId = 1,
            PhoneNumber = "0900000001",
            IsVip = false
        });
        await db.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = CreateUserManager(new ApplicationUser
        {
            Id = "u-normal",
            ClientId = 12
        });

        (bool isVip, string? note) = await CurrentClientVipLookup.ResolveAsync(
            userManager.Object,
            db,
            Principal(RoleNames.Client));

        Assert.False(isVip);
        Assert.Null(note);
    }

    private static ClaimsPrincipal Principal(string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "u"), new Claim(ClaimTypes.Role, role)],
            "Test"));
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(ApplicationUser user)
    {
        Mock<UserManager<ApplicationUser>> mock = new(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        return mock;
    }

    private static ApplicationDbContext CreateDb()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new ApplicationDbContext(options);
    }
}
