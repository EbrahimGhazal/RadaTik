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

        HttpRequest agentRequest = CreateRequest("/Account/login", "Mozilla/5.0 RadaTikNative/collection Capacitor");
        Assert.Equal(NativeAppContext.Collection, NativeAppContext.Detect(agentRequest));

        HttpRequest pathRequest = CreateRequest("/clientPortal/dashboard", "Mozilla/5.0 Capacitor/7.0");
        Assert.Equal(NativeAppContext.Client, NativeAppContext.Detect(pathRequest));

        HttpRequest browserRequest = CreateRequest("/clientPortal/dashboard", "Mozilla/5.0 Chrome/120");
        Assert.Null(NativeAppContext.Detect(browserRequest));
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

    private static HttpRequest CreateRequest(string path, string userAgent, string query = "")
    {
        DefaultHttpContext context = new();
        context.Request.Path = path;
        context.Request.QueryString = new QueryString(query);
        context.Request.Headers.UserAgent = userAgent;
        return context.Request;
    }
}
