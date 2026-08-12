using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using RadaTik.Areas.CompanyAdmin.Controllers;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.Account;
using System.Security.Claims;
using Xunit;

namespace RadaTik.Tests.CompanyAdmin;

public sealed class CompanyAdminAccountControllerTests
{
    [Fact]
    public async Task UpdateProfile_WhenSuccessful_RedirectsToNetworkManagerProfileWithSavedFlag()
    {
        using ApplicationDbContext db = CreateDbContext();
        ApplicationUser user = new()
        {
            Id = "manager-1",
            UserName = "manager",
            Email = "old@example.com",
            FullName = "Manager",
            NetworkId = 1
        };

        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(user);
        userManager.Setup(m => m.GetRolesAsync(user)).ReturnsAsync([RoleNames.NetworkAdministrator]);
        userManager.Setup(m => m.SetEmailAsync(user, "new@example.com"))
            .Callback<ApplicationUser, string?>((u, email) => u.Email = email)
            .ReturnsAsync(IdentityResult.Success);
        userManager.Setup(m => m.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        Mock<SignInManager<ApplicationUser>> signInManager = CreateSignInManagerMock(userManager);

        AccountController controller = new(
            userManager.Object,
            signInManager.Object,
            Mock.Of<Microsoft.AspNetCore.Hosting.IWebHostEnvironment>());

        DefaultHttpContext httpContext = new();
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, user.Id)],
            "Test"));
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        ProfileViewModel model = new()
        {
            Email = "new@example.com",
            PhoneNumber = user.PhoneNumber
        };

        IActionResult result = await controller.UpdateProfile(model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Profile", view.ViewName);
        Assert.True(controller.ViewData["ShowSaveSuccess"] as bool?);
        ProfileViewModel returnedModel = Assert.IsType<ProfileViewModel>(view.Model);
        Assert.Equal("new@example.com", returnedModel.Email);
        signInManager.Verify(m => m.RefreshSignInAsync(It.IsAny<ApplicationUser>()), Times.Never);
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(ApplicationUser user)
    {
        Mock<UserManager<ApplicationUser>> mock = new(
            Mock.Of<IUserStore<ApplicationUser>>(),
            null!, null!, null!, null!, null!, null!, null!, null!);
        mock.Setup(m => m.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        return mock;
    }

    private static Mock<SignInManager<ApplicationUser>> CreateSignInManagerMock(Mock<UserManager<ApplicationUser>> userManager)
    {
        Mock<IHttpContextAccessor> contextAccessor = new();
        Mock<SignInManager<ApplicationUser>> signInManager = new(
            userManager.Object,
            contextAccessor.Object,
            Mock.Of<IUserClaimsPrincipalFactory<ApplicationUser>>(),
            null!, null!, null!, null!);
        signInManager.Setup(m => m.RefreshSignInAsync(It.IsAny<ApplicationUser>())).Returns(Task.CompletedTask);
        return signInManager;
    }

    private static ApplicationDbContext CreateDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
