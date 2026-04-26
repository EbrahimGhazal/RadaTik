using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using RadTik.Areas.CompanyAdmin.Controllers;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.ViewModels.CollectionPoints;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Xunit;

namespace RadTik.Tests.CompanyAdmin;

public class CollectionPointsControllerTests
{
    [Fact]
    public async Task Create_WithNegativeInitialBalance_ReturnsViewWithModelError()
    {
        using var context = CreateDbContext();
        var user = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var userManager = CreateUserManagerMock(user);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();

        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var model = new CreateCollectionPointViewModel
        {
            UserName = "cp01",
            Email = "cp01@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point 01",
            InitialBalance = -10m
        };

        var result = await controller.Create(model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.True(controller.ModelState.ErrorCount > 0);
        Assert.Contains(controller.ModelState, e => e.Value?.Errors.Any(er => er.ErrorMessage.Contains("سالب")) == true);
    }

    [Fact]
    public async Task Edit_WithNegativeNewBalance_ReturnsViewWithModelError()
    {
        using var context = CreateDbContext();
        var user = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var userManager = CreateUserManagerMock(user);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();

        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var model = new EditCollectionPointViewModel
        {
            Id = 10,
            UserId = "cp-user",
            UserName = "cp01",
            CurrentBalance = 100m,
            NewBalance = -5m
        };

        var result = await controller.Edit(10, model);

        var view = Assert.IsType<ViewResult>(result);
        Assert.Same(model, view.Model);
        Assert.True(controller.ModelState.ErrorCount > 0);
        Assert.Contains(controller.ModelState, e => e.Value?.Errors.Any(er => er.ErrorMessage.Contains("سالب")) == true);
    }

    [Fact]
    public void CreateViewModel_WithZeroInitialBalance_PassesValidation()
    {
        var model = new CreateCollectionPointViewModel
        {
            UserName = "cp02",
            Email = "cp02@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point 02",
            InitialBalance = 0m
        };

        var context = new ValidationContext(model);
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(model, context, results, true);

        Assert.True(isValid);
    }

    [Fact]
    public async Task Edit_WithPositiveNewBalance_UpdatesAccountAndRedirects()
    {
        using var context = CreateDbContext();
        var user = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var pointUser = new ApplicationUser { Id = "cp-user", UserName = "cp01", NetworkId = 1 };

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

        var userManager = CreateUserManagerMock(user);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();
        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var model = new EditCollectionPointViewModel
        {
            Id = 10,
            UserId = "cp-user",
            UserName = "cp01",
            CurrentBalance = 100m,
            NewBalance = 250m
        };

        var result = await controller.Edit(10, model);

        ExpectRedirectToIndex(result, controller);

        var account = await context.CollectionPointAccounts.FirstAsync(a => a.Id == 10);
        Assert.Equal(250m, account.Balance);
    }

    [Fact]
    public async Task Create_WithZeroInitialBalance_CreatesAccountWithoutWalletDeduction()
    {
        using var context = CreateDbContext();
        var manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var userManager = CreateUserManagerMock(manager);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();
        usageService
            .Setup(s => s.ChargeUsageIncreaseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PricingChargeUnit?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var model = new CreateCollectionPointViewModel
        {
            UserName = "cp-zero",
            Email = "cp-zero@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point Zero",
            InitialBalance = 0m
        };

        var result = await controller.Create(model);

        ExpectRedirectToIndex(result, controller);

        var created = await context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == "cp-created");
        Assert.NotNull(created);
        Assert.Equal(0m, created!.Balance);

        var network = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(1000m, network.Balance);
        Assert.Empty(context.NetworkWalletTransactions);
    }

    [Fact]
    public async Task Create_WithPositiveInitialBalance_DeductsWalletAndCreatesTransaction()
    {
        using var context = CreateDbContext();
        var manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var userManager = CreateUserManagerMock(manager);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();
        usageService
            .Setup(s => s.ChargeUsageIncreaseAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<PricingChargeUnit?>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var model = new CreateCollectionPointViewModel
        {
            UserName = "cp-positive",
            Email = "cp-positive@example.com",
            Password = "P@ssw0rd!",
            ConfirmPassword = "P@ssw0rd!",
            FullName = "Collection Point Positive",
            InitialBalance = 250m
        };

        var result = await controller.Create(model);

        ExpectRedirectToIndex(result, controller);

        var created = await context.CollectionPointAccounts.FirstOrDefaultAsync(a => a.UserId == "cp-created");
        Assert.NotNull(created);
        Assert.Equal(250m, created!.Balance);

        var network = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(750m, network.Balance);

        var tx = await context.NetworkWalletTransactions.SingleAsync();
        Assert.Equal(NetworkWalletTransactionType.Adjustment, tx.Type);
        Assert.Equal(-250m, tx.SignedAmount);
        Assert.Equal(1000m, tx.PreviousBalance);
        Assert.Equal(750m, tx.NewBalance);
        Assert.Equal("manager-1", tx.CreatedByUserId);

        usageService.Verify(s => s.ChargeUsageIncreaseAsync(1, "manager-1", PricingChargeUnit.PerCollectionPoint, It.IsAny<CancellationToken>()), Times.Once);
        usageService.Verify(s => s.ChargeUsageIncreaseAsync(1, "manager-1", PricingChargeUnit.PerUser, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveTopUpRequest_WithSufficientBalance_UpdatesBalancesAndCreatesWalletTransaction()
    {
        using var context = CreateDbContext();
        var manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var pointUser = new ApplicationUser { Id = "cp-user", UserName = "cp01", NetworkId = 1 };

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

        var userManager = CreateUserManagerMock(manager);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();
        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var result = await controller.ApproveTopUpRequest(70, "ok");

        ExpectRedirectToAction(result, "TopUpRequests", controller);

        var req = await context.CollectionPointTopUpRequests.FirstAsync(r => r.Id == 70);
        Assert.Equal(CollectionPointTopUpStatus.Approved, req.Status);
        Assert.Equal("manager-1", req.ProcessedByUserId);
        Assert.NotNull(req.ProcessedAt);

        var account = await context.CollectionPointAccounts.FirstAsync(a => a.Id == 50);
        Assert.Equal(140m, account.Balance);

        var network = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(900m, network.Balance);

        var tx = await context.NetworkWalletTransactions.SingleAsync();
        Assert.Equal(NetworkWalletTransactionType.Adjustment, tx.Type);
        Assert.Equal(-100m, tx.SignedAmount);
        Assert.Equal(1000m, tx.PreviousBalance);
        Assert.Equal(900m, tx.NewBalance);
    }

    [Fact]
    public async Task ApproveTopUpRequest_WithInsufficientBalance_DoesNotApplyChanges()
    {
        using var context = CreateDbContext();
        var manager = new ApplicationUser { Id = "manager-1", UserName = "manager", NetworkId = 1 };
        var pointUser = new ApplicationUser { Id = "cp-user", UserName = "cp01", NetworkId = 1 };

        var network = await context.Networks.FirstAsync(n => n.Id == 1);
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

        var userManager = CreateUserManagerMock(manager);
        var usageService = new Mock<IUsageBasedSubscriptionChargeService>();
        var controller = new CollectionPointsController(
            context,
            userManager.Object,
            Mock.Of<ILogger<CollectionPointsController>>(),
            usageService.Object);
        AttachHttpContext(controller);

        var result = await controller.ApproveTopUpRequest(71, null);

        ExpectRedirectToAction(result, "TopUpRequests", controller);

        var req = await context.CollectionPointTopUpRequests.FirstAsync(r => r.Id == 71);
        Assert.Equal(CollectionPointTopUpStatus.Pending, req.Status);
        Assert.Null(req.ProcessedAt);
        Assert.Null(req.ProcessedByUserId);

        var account = await context.CollectionPointAccounts.FirstAsync(a => a.Id == 51);
        Assert.Equal(10m, account.Balance);

        var unchangedNetwork = await context.Networks.FirstAsync(n => n.Id == 1);
        Assert.Equal(30m, unchangedNetwork.Balance);
        Assert.Empty(context.NetworkWalletTransactions);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        var context = new ApplicationDbContext(options);
        context.Networks.Add(new Network { Id = 1, Name = "Main Network", Balance = 1000m, ManagerUserId = "manager-1" });
        context.SaveChanges();
        return context;
    }

    private static Mock<UserManager<ApplicationUser>> CreateUserManagerMock(ApplicationUser user)
    {
        var store = new Mock<IUserStore<ApplicationUser>>();
        var mock = new Mock<UserManager<ApplicationUser>>(
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
        var context = new DefaultHttpContext();
        context.Session = new TestSession();
        return context;
    }

    private static void AttachHttpContext(Controller controller)
    {
        var httpContext = CreateHttpContext();
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
            var errors = controller.ModelState
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
            var errors = controller.ModelState
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
