using Microsoft.AspNetCore.Http;
using RadaTik.Security;
using Xunit;

namespace RadaTik.Tests.Security;

public sealed class NativeAppContextTests
{
    [Theory]
    [InlineData("client", NativeAppContext.Client)]
    [InlineData("COLLECTION", NativeAppContext.Collection)]
    [InlineData("employee", NativeAppContext.Employee)]
    [InlineData("other", null)]
    public void Normalize_MapsKnownAppAliases(string? input, string? expected)
    {
        Assert.Equal(expected, NativeAppContext.Normalize(input));
    }

    [Fact]
    public void Detect_UsesQueryThenUserAgentThenNativePath()
    {
        HttpRequest queryRequest = CreateRequest("/Account/login", "Mozilla/5.0", "?app=employee");
        Assert.Equal(NativeAppContext.Employee, NativeAppContext.Detect(queryRequest));

        HttpRequest agentRequest = CreateRequest("/Account/login", "Mozilla/5.0 RadaTikNative/collection/2 Capacitor");
        Assert.Equal(NativeAppContext.Collection, NativeAppContext.Detect(agentRequest));

        HttpRequest pathRequest = CreateRequest("/clientPortal/dashboard", "Mozilla/5.0 Capacitor/7.0");
        Assert.Equal(NativeAppContext.Client, NativeAppContext.Detect(pathRequest));

        HttpRequest browserRequest = CreateRequest("/clientPortal/dashboard", "Mozilla/5.0 Chrome/120");
        Assert.Null(NativeAppContext.Detect(browserRequest));
    }

    [Fact]
    public void AccountController_KeepsNativeAppSessionsPersistent()
    {
        string controller = File.ReadAllText(FindFile("RadaTik", "Controllers", "AccountController.cs"));
        Assert.Contains("persistSession", controller);
        Assert.Contains("nativeApp != null", controller);

        string program = File.ReadAllText(FindFile("RadaTik", "Program.cs"));
        Assert.Contains("OnSigningIn", program);
        Assert.Contains("IsPersistent = true", program);
        Assert.Contains("AddDays(90)", program);

        string collection = File.ReadAllText(FindFile("RadaTik", "Areas", "CollectionPoint", "Controllers", "CollectionPointController.cs"));
        Assert.Contains("UnreadNotificationsCount", collection);
        Assert.Contains("UserNotifications", collection);
    }

    [Fact]
    public void ClientsController_AcceptsNationalIdUploads()
    {
        string controller = File.ReadAllText(FindFile("RadaTik", "Controllers", "ClientsController.NationalId.cs"));
        Assert.Contains("UploadNationalId", controller);
        Assert.Contains("NationalIdFrontPath", controller);
        Assert.Contains("NationalIdBackPath", controller);
    }

    [Theory]
    [InlineData("Mozilla/5.0 RadaTikNative/client Capacitor", 0, true)]
    [InlineData("Mozilla/5.0 RadaTikNative/client/1 Capacitor", 1, true)]
    [InlineData("Mozilla/5.0 RadaTikNative/employee/2 Capacitor", 2, false)]
    [InlineData("Mozilla/5.0 Chrome/120", 0, false)]
    public void NativeVersion_BlocksOldShellsOnly(string userAgent, int expectedVersion, bool outdated)
    {
        Assert.Equal(expectedVersion, NativeAppContext.ReadVersion(userAgent));
        Assert.Equal(outdated, NativeAppContext.IsNativeAppOutdated(userAgent));
        Assert.True(NativeAppContext.IsVersionGateExempt("/Account/AppUpdateRequired"));
        Assert.True(NativeAppContext.IsVersionGateExempt("/RadaTik/DownloadAndroid"));
        Assert.False(NativeAppContext.IsVersionGateExempt("/clientPortal/dashboard"));
    }

    [Fact]
    public void AccountController_ExposesAppUpdateRequiredPage()
    {
        string controller = File.ReadAllText(FindFile("RadaTik", "Controllers", "AccountController.cs"));
        Assert.Contains("AppUpdateRequired", controller);
        Assert.Contains("NativeAppContext.DownloadPath", controller);

        string middleware = File.ReadAllText(FindFile("RadaTik", "Middleware", "NativeAppRoleMiddleware.cs"));
        Assert.Contains("IsNativeAppOutdated", middleware);
        Assert.Contains("/Account/AppUpdateRequired", middleware);

        string view = File.ReadAllText(FindFile("RadaTik", "Views", "Account", "AppUpdateRequired.cshtml"));
        Assert.Contains("هذا الإصدار لم يعد مدعوماً", view);
        Assert.Contains("تحميل التحديث", view);
    }

    [Fact]
    public void IsRoleAllowed_LocksEachAppToItsRole()
    {
        Assert.True(NativeAppContext.IsRoleAllowed(null, [RoleNames.SystemAdministrator]));
        Assert.True(NativeAppContext.IsRoleAllowed(NativeAppContext.Client, [RoleNames.Client]));
        Assert.False(NativeAppContext.IsRoleAllowed(NativeAppContext.Client, [RoleNames.CollectionPoint]));
        Assert.True(NativeAppContext.IsRoleAllowed(NativeAppContext.Collection, [RoleNames.CollectionPoint]));
        Assert.False(NativeAppContext.IsRoleAllowed(NativeAppContext.Collection, [RoleNames.Client]));
        Assert.True(NativeAppContext.IsRoleAllowed(NativeAppContext.Employee, [RoleNames.CompanyEmployee]));
        Assert.True(NativeAppContext.IsRoleAllowed(NativeAppContext.Employee, [RoleNames.EmployeeLegacy]));
        Assert.False(NativeAppContext.IsRoleAllowed(NativeAppContext.Employee, [RoleNames.Client]));
    }

    [Theory]
    [InlineData("/", true)]
    [InlineData("/RadaTik", true)]
    [InlineData("/radatik/Apps", true)]
    [InlineData("/RadaTik/DownloadAndroid", false)]
    [InlineData("/clientPortal/dashboard", false)]
    [InlineData("/css/main.css", false)]
    public void PublicVisitorPath_CountsMarketingPagesOnly(string path, bool expected)
    {
        Assert.Equal(expected, RadaTik.Middleware.PublicVisitorCounterMiddleware.IsPublicSitePath(path));
    }

    private static string FindFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }

    private static HttpRequest CreateRequest(string path, string userAgent, string query = "")
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Request.Headers.UserAgent = userAgent;
        return context.Request;
    }
}
