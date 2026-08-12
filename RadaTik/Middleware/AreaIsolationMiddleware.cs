using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using RadaTik.Security;

namespace RadaTik.Middleware;

/// <summary>
/// Prevent switching between Areas while authenticated, unless the user logs out and logs in again.
/// We lock an "ActiveArea" in Session on first area request (or during login), then block other areas.
/// مدير النظام ومدير الشركة يمكنهم الوصول لأي منطقة (لإدارة نقاط التحصيل والعملاء وغيرها).
/// </summary>
public sealed class AreaIsolationMiddleware(RequestDelegate _next, ILogger<AreaIsolationMiddleware> _logger)
{
    public const string SessionKeyActiveArea = "ActiveArea";

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        // Always allow auth endpoints.
        var path = context.Request.Path.Value ?? "";
        if (path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        // مدير النظام ومدير الشركة يمكنهم الوصول لأي منطقة
        if (context.User.IsInRole(RoleNames.SystemAdministrator) || context.User.IsInRole(RoleNames.NetworkAdministrator))
        {
            await _next(context);
            return;
        }

        var requestedArea = GetRequestedArea(context);
        if (string.IsNullOrWhiteSpace(requestedArea))
        {
            await _next(context);
            return;
        }

        var activeArea = context.Session.GetString(SessionKeyActiveArea);
        if (string.IsNullOrWhiteSpace(activeArea))
        {
            context.Session.SetString(SessionKeyActiveArea, requestedArea);
            await _next(context);
            return;
        }

        if (!string.Equals(activeArea, requestedArea, StringComparison.OrdinalIgnoreCase))
        {
            // موظف الشركة: عند الضغط على رابط networkManager بالخطأ
            // نحاول تحويله تلقائياً إلى المسار المناظر في /employee بدل صفحة AccessDenied.
            var isEmployeeToCompanyAdminSwitch =
                string.Equals(activeArea, "CompanyEmployee", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(requestedArea, "CompanyAdmin", StringComparison.OrdinalIgnoreCase);

            if (isEmployeeToCompanyAdminSwitch &&
                TryMapCompanyAdminPathToEmployee(path, context.Request.QueryString.Value, out var employeePath))
            {
                _logger.LogInformation(
                    "Area switch remapped for employee user {UserName}: {FromPath} -> {ToPath}",
                    context.User.Identity?.Name ?? "unknown",
                    path + (context.Request.QueryString.Value ?? string.Empty),
                    employeePath);
                context.Response.Redirect(employeePath);
                return;
            }

            if (isEmployeeToCompanyAdminSwitch)
            {
                _logger.LogWarning(
                    "Area switch blocked (no employee mapping) for user {UserName}: {RequestedPath}",
                    context.User.Identity?.Name ?? "unknown",
                    path + (context.Request.QueryString.Value ?? string.Empty));
            }

            // Block switching areas without re-login.
            var redirectUrl =
                $"/Account/accessDenied?reason=areaSwitch&activeArea={Uri.EscapeDataString(activeArea)}&requestedArea={Uri.EscapeDataString(requestedArea)}";

            context.Response.Redirect(redirectUrl);
            return;
        }

        await _next(context);
    }

    private static string? GetRequestedArea(HttpContext context)
    {
        // Prefer routing "area" value when present.
        var routeData = context.GetRouteData();
        if (routeData?.Values != null && routeData.Values.TryGetValue("area", out var areaObj))
        {
            var area = areaObj?.ToString();
            if (!string.IsNullOrWhiteSpace(area))
            {
                return area;
            }
        }

        // Fall back to clean URL prefixes that represent an Area in this app.
        var p = context.Request.Path.Value ?? "";
        if (p.StartsWith("/networkManager", StringComparison.OrdinalIgnoreCase)) return "CompanyAdmin";
        if (p.StartsWith("/systemAdmin", StringComparison.OrdinalIgnoreCase)) return "SystemAdmin";
        if (p.StartsWith("/employee", StringComparison.OrdinalIgnoreCase)) return "CompanyEmployee";
        if (p.StartsWith("/clientPortal", StringComparison.OrdinalIgnoreCase)) return "ClientPortal";
        if (p.StartsWith("/collectionPoint", StringComparison.OrdinalIgnoreCase)) return "CollectionPoint";

        return null;
    }

    private static bool TryMapCompanyAdminPathToEmployee(string path, string? queryString, out string mappedPath)
    {
        mappedPath = string.Empty;
        if (!path.StartsWith("/networkManager", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var relative = path["/networkManager".Length..];
        if (string.IsNullOrWhiteSpace(relative))
        {
            mappedPath = "/employee/dashboard" + (queryString ?? string.Empty);
            return true;
        }

        // نعيد التوجيه فقط للمسارات التي لها مقابل واضح في بوابة الموظف.
        var allowedEmployeeSegments = new[]
        {
            "/dashboard",
            "/Sector",
            "/Receiver",
            "/RequestsManagement",
            "/Clients",
            "/Network"
        };

        var isAllowed = allowedEmployeeSegments.Any(seg =>
            relative.StartsWith(seg, StringComparison.OrdinalIgnoreCase));
        if (!isAllowed)
        {
            return false;
        }

        mappedPath = "/employee" + relative + (queryString ?? string.Empty);
        return true;
    }
}

