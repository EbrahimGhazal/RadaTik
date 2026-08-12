using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RadaTik.Areas.CompanyAdmin.Controllers;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.ViewModels.CollectionPoints;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Xunit;

namespace RadaTik.Tests.CompanyAdmin;

public class CollectionPointsControllerTests
{
    [Fact]
    public async Task Create_WhenCalledFromCompanyAdmin_RedirectsToIndex()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser user = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(user);

        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        CreateCollectionPointViewModel model = new CreateCollectionPointViewModel
        {
            UserName = "cp01",
            Email = "cp01@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point 01",
            InitialBalance = -10m
        };

        IActionResult result = controller.Create(model);

        RedirectToActionResult redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task Edit_WithNegativeNewBalance_ReturnsViewWithModelError()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser user = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(user);

        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        EditCollectionPointViewModel model = new EditCollectionPointViewModel
        {
            Id = 10,
            UserId = "cp-user",
            UserName = "cp01",
            CurrentBalance = 100m,
            NewBalance = -5m
        };

        IActionResult result = await controller.Edit(10, model);

        ViewResult view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.True(controller.ModelState.ErrorCount > 0);
        Assert.Contains(controller.ModelState, e => e.Value?.Errors.Any(er => er.ErrorMessage.Contains("سالب")) == true);
    }

    [Fact]
    public void CreateViewModel_WithZeroInitialBalance_PassesValidation()
    {
        CreateCollectionPointViewModel model = new CreateCollectionPointViewModel
        {
            UserName = "cp02",
            Email = "cp02@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point 02",
            InitialBalance = 0m
        };

        ValidationContext context = new ValidationContext(model);
        List<ValidationResult> results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(model, context, results, true);

        Assert.True(isValid);
    }

    [Fact]
    public async Task Edit_WithPositiveNewBalance_UpdatesAccountAndRedirects()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser user = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        ApplicationUser pointUser = new ApplicationUser { Id = "cp-user", UserName = "cp01", NetworkId = 1 };

        context.Users.Add(pointUser);
        context.CollectionPointAccounts.Add(new CollectionPointAccount
        {
            Id = 10,
            UserId = pointUser.Id,
            NetworkId = 1,
            Balance = 100m,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(user);
        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        EditCollectionPointViewModel model = new EditCollectionPointViewModel
        {
            Id = 10,
            UserId = "cp-user",
            UserName = "cp01",
            CurrentBalance = 100m,
            NewBalance = 250m
        };

        IActionResult result = await controller.Edit(10, model);

        ExpectRedirectToIndex(result, controller);

        CollectionPointAccount account = await context.CollectionPointAccounts.FirstAsync(a => a.Id == 10);
        Assert.Equal(250m, account.Balance);
    }

    [Fact]
    public async Task Create_WithZeroInitialBalance_DoesNotCreateAccountInCompanyAdminFlow()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(manager);

        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        CreateCollectionPointViewModel model = new CreateCollectionPointViewModel
        {
            UserName = "cp-zero",
            Email = "cp-zero@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point Zero",
            InitialBalance = 0m
        };

        IActionResult result = controller.Create(model);

        ExpectRedirectToIndex(result, controller);

        CollectionPointAccount? created = await context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == "cp-created");
        Assert.Null(created);

        Network network = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(1000m, network.Balance);
        Assert.Empty(context.NetworkWalletTransactions);
    }

    [Fact]
    public async Task Create_WithPositiveInitialBalance_DoesNotDeductWalletInCompanyAdminFlow()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(manager);

        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        CreateCollectionPointViewModel model = new CreateCollectionPointViewModel
        {
            UserName = "cp-positive",
            Email = "cp-positive@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point Positive",
            InitialBalance = 250m
        };

        IActionResult result = controller.Create(model);

        ExpectRedirectToIndex(result, controller);

        CollectionPointAccount? created = await context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == "cp-created");
        Assert.Null(created);

        Network network = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(1000m, network.Balance);
        Assert.Empty(context.NetworkWalletTransactions);
    }

    [Fact]
    public async Task ApproveTopUpRequest_FromCompanyAdmin_RedirectsIndexWithoutApplyingChanges()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        ApplicationUser pointUser = new ApplicationUser { Id = "cp-user", UserName = "cp01", NetworkId = 1 };

        context.Users.Add(pointUser);
        context.CollectionPointAccounts.Add(new CollectionPointAccount
        {
            Id = 50,
            UserId = pointUser.Id,
            NetworkId = 1,
            Balance = 40m,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        context.CollectionPointTopUpRequests.Add(new CollectionPointTopUpRequest
        {
            Id = 70,
            CollectionPointAccountId = 50,
            RequestTargetType = CollectionPointTopUpTarget.CompanyManager,
            TargetNetworkId = 1,
            Amount = 100m,
            ReferenceNumber = "REF-100",
            ReceiptImagePath = "/uploads/receipts/100.jpg",
            Status = CollectionPointTopUpStatus.Pending,
            RequestedByUserId = pointUser.Id,
            RequestedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(manager);
        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        IActionResult result = controller.ApproveTopUpRequest(70, "ok");

        ExpectRedirectToAction(result, "Index", controller);

        CollectionPointTopUpRequest req = await context.CollectionPointTopUpRequests.FirstAsync(r => r.Id == 70);
        Assert.Equal(CollectionPointTopUpStatus.Pending, req.Status);
        Assert.Null(req.ProcessedByUserId);
        Assert.Null(req.ProcessedAt);

        CollectionPointAccount account = await context.CollectionPointAccounts.FirstAsync(a => a.Id == 50);
        Assert.Equal(40m, account.Balance);

        Network network = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(1000m, network.Balance);
        Assert.Empty(context.NetworkWalletTransactions);
    }

    [Fact]
    public async Task ApproveTopUpRequest_WithInsufficientBalance_StillRedirectsIndexAndDoesNotApplyChanges()
    {
        using ApplicationDbContext context = CreateDbContext();
        ApplicationUser manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        ApplicationUser pointUser = new ApplicationUser { Id = "cp-user", UserName = "cp01", NetworkId = 1 };

        Network network = await context.Networks.FirstAsync(n => n.Id == 1);
        network.Balance = 30m;

        context.Users.Add(pointUser);
        context.CollectionPointAccounts.Add(new CollectionPointAccount
        {
            Id = 51,
            UserId = pointUser.Id,
            NetworkId = 1,
            Balance = 10m,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        });
        context.CollectionPointTopUpRequests.Add(new CollectionPointTopUpRequest
        {
            Id = 71,
            CollectionPointAccountId = 51,
            RequestTargetType = CollectionPointTopUpTarget.CompanyManager,
            TargetNetworkId = 1,
            Amount = 100m,
            ReferenceNumber = "REF-200",
            ReceiptImagePath = "/uploads/receipts/200.jpg",
            Status = CollectionPointTopUpStatus.Pending,
            RequestedByUserId = pointUser.Id,
            RequestedAt = DateTime.Now
        });
        await context.SaveChangesAsync();

        Mock<UserManager<ApplicationUser>> userManager = CreateUserManagerMock(manager);
        CollectionPointsController controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>());
        AttachHttpContext(controller);

        IActionResult result = controller.ApproveTopUpRequest(71, null);

        ExpectRedirectToAction(result, "Index", controller);

        CollectionPointTopUpRequest req = await context.CollectionPointTopUpRequests.FirstAsync(r => r.Id == 71);
        Assert.Equal(CollectionPointTopUpStatus.Pending, req.Status);
        Assert.Null(req.ProcessedAt);
        Assert.Null(req.ProcessedByUserId);

        CollectionPointAccount account = await context.CollectionPointAccounts.FirstAsync(a => a.Id == 51);
        Assert.Equal(10m, account.Balance);

        Network unchangedNetwork = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(30m, unchangedNetwork.Balance);
        Assert.Empty(context.NetworkWalletTransactions);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        DbContextOptions<ApplicationDbContext> options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        ApplicationDbContext context = new ApplicationDbContext(options);
        context.Networks.Add(new Network { Id = 1, Name = "Main Network", Balance = 1000m, ManagerUserId = "manager-1" });
        context.SaveChanges();
        return context;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(ApplicationUser user)
    {
        Mock<IUserStore<ApplicationUser>> store = new Mock<IUserStore<ApplicationUser>>();
        Mock<UserManager<ApplicationUser>> mock = new Mock<UserManager<ApplicationUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        mock.Setup(x => x.GetUserAsync(It.IsAny<ClaimsPrincipal>())).ReturnsAsync(user);
        mock.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .Callback<ApplicationUser, string>((u, _) => u.Id = "cp-created")
            .ReturnsAsync(IdentityResult.Success);
        mock.Setup(x => x.IsInRoleAsync(It.IsAny<ApplicationUser>(), RoleNames.CollectionPoint))
            .ReturnsAsync(false);
        mock.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), RoleNames.CollectionPoint))
            .ReturnsAsync(IdentityResult.Success);
        return mock;
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        DefaultHttpContext context = new DefaultHttpContext();
        context.Session = new TestSession();
        return context;
    }

    private static void AttachHttpContext(Controller controller)
    {
        DefaultHttpContext httpContext = CreateHttpContext();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
        controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
    }

    private static RedirectToActionResult ExpectRedirectToIndex(IActionResult result, Controller controller)
    {
        if (result is RedirectToActionResult redirect)
        {
            Assert.Equal("Index", redirect.ActionName);
            return redirect;
        }

        if (result is ViewResult)
        {
            List<string> errors = controller.ModelState
                .SelectMany(kv => kv.Value?.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}") ?? [])
                .ToList();
            Assert.Fail($"Expected redirect but got ViewResult. ModelState errors: {string.Join(" | ", errors)}");
        }

        Assert.Fail($"Expected RedirectToActionResult but got {result.GetType().Name}.");
        throw new InvalidOperationException();
    }

    private static RedirectToActionResult ExpectRedirectToAction(IActionResult result, string actionName, Controller controller)
    {
        if (result is RedirectToActionResult redirect)
        {
            Assert.Equal(actionName, redirect.ActionName);
            return redirect;
        }

        if (result is ViewResult)
        {
            List<string> errors = controller.ModelState
                .SelectMany(kv => kv.Value?.Errors.Select(e => $"{kv.Key}: {e.ErrorMessage}") ?? [])
                .ToList();
            Assert.Fail($"Expected redirect to '{actionName}' but got ViewResult. ModelState errors: {string.Join(" | ", errors)}");
        }

        Assert.Fail($"Expected RedirectToActionResult('{actionName}') but got {result.GetType().Name}.");
        throw new InvalidOperationException();
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
