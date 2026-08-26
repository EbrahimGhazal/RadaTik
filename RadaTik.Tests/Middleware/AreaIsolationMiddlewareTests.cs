using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using RadaTik.Middleware;
using Xunit;

namespace RadaTik.Tests.Middleware;

public sealed class AreaIsolationMiddlewareTests
{
    [Theory]
    [InlineData("/networkManager/Account/UpdateProfile", "/employee/Account/UpdateProfile")]
    [InlineData("/networkManager/Account/ChangePassword", "/employee/Account/ChangePassword")]
    [InlineData("/networkManager/Clients", "/employee/Clients")]
    public void EmployeeAreaRemap_MapsCompanyAdminAccountPathsToEmployee(string fromPath, string expected)
    {
        bool mapped = InvokeMap(fromPath, null, out string mappedPath);
        Assert.True(mapped);
        Assert.Equal(expected, mappedPath);
    }

    private static bool InvokeMap(string path, string? query, out string mappedPath)
    {
        System.Reflection.MethodInfo? method = typeof(AreaIsolationMiddleware)
            .GetMethod("TryMapCompanyAdminPathToEmployee", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.NotNull(method);

        object?[] args = [path, query, null];
        bool result = (bool)method!.Invoke(null, args)!;
        mappedPath = (string)args[2]!;
        return result;
    }
}
