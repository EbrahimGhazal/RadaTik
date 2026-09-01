using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.Company;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class CompanyClientPresenceServiceTests
{
    [Fact]
    public async Task AddSocial_ThenHide_RemovesItFromClientSnapshot()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 10, Name = "شركة النور" });
        db.Profiles.Add(new Profile { Id = 1, Name = "P", NetworkId = 10 });
        db.Clients.Add(new Client
        {
            Id = 4,
            Name = "مشترك",
            UserName = "c4",
            Password = "p",
            SID = "4",
            PhoneNumber = "0",
            ProfileId = 1,
            NetworkId = 10
        });
        await db.SaveChangesAsync();

        ApplicationUser user = new() { Id = "u1", UserName = "c4", ClientId = 4 };
        CompanyClientPresenceService sut = new(db, CreateUserManager(user).Object);

        (bool added, string addMessage) = await sut.AddSocialAsync(10, new CompanySocialLinkSaveCommand
        {
            Platform = SocialMediaPlatform.Facebook,
            Url = "https://facebook.com/nour",
            IsVisibleToClients = true
        });
        Assert.True(added, addMessage);

        CompanyClientPresenceSnapshot visible = await sut.GetForCurrentClientAsync(SignedIn());
        Assert.True(visible.HasSocialLinks);
        Assert.Equal("شركة النور", visible.CompanyName);

        int id = visible.VisibleSocialLinks[0].Id;
        (bool hidden, _) = await sut.ToggleSocialAsync(10, id);
        Assert.True(hidden);

        CompanyClientPresenceSnapshot afterHide = await sut.GetForCurrentClientAsync(SignedIn());
        Assert.False(afterHide.HasSocialLinks);
    }

    [Fact]
    public async Task ComplaintNumbers_AreScopedToCompanyAndVisibility()
    {
        await using ApplicationDbContext db = CreateDb();
        db.Networks.Add(new Network { Id = 3, Name = "شركة" });
        db.Networks.Add(new Network { Id = 4, Name = "فرع", ParentNetworkId = 3 });
        await db.SaveChangesAsync();

        CompanyClientPresenceService sut = new(db, CreateUserManager(new ApplicationUser()).Object);
        (bool added, string message) = await sut.AddComplaintAsync(4, new CompanyComplaintContactSaveCommand
        {
            Label = "شكاوى فنية",
            PhoneNumber = "0991234567",
            IsVisibleToClients = false
        });
        Assert.True(added, message);

        CompanyClientPresenceAdminPage? page = await sut.GetAdminPageAsync(4, "complaints");
        Assert.NotNull(page);
        Assert.Equal(3, page.CompanyNetworkId);
        Assert.Single(page.ComplaintContacts);
        Assert.False(page.ComplaintContacts[0].IsVisibleToClients);
        Assert.Equal("0991234567", page.ComplaintContacts[0].PhoneNumber);
    }

    [Fact]
    public async Task GetForCurrentClient_Anonymous_ReturnsEmpty()
    {
        await using ApplicationDbContext db = CreateDb();
        CompanyClientPresenceService sut = new(db, CreateUserManager(new ApplicationUser()).Object);
        CompanyClientPresenceSnapshot snapshot = await sut.GetForCurrentClientAsync(new ClaimsPrincipal());
        Assert.False(snapshot.HasSocialLinks);
        Assert.False(snapshot.HasComplaintContacts);
    }

    private static ClaimsPrincipal SignedIn() =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, "c4")], "test"));

    private static Mock<UserManager<ApplicationUser>> CreateUserManager(ApplicationUser user)
    {
        Mock<IUserStore<ApplicationUser>> store = new();
        Mock<UserManager<ApplicationUser>> mock = new(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
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
