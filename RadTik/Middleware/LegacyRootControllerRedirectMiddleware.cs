using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using RadTik.Security;

namespace RadTik.Middleware;

/// <summary>
/// Redirect legacy root-controller URLs (no Area) to the correct Area routes.
/// This prevents role pages from being accessible outside their Area and keeps old bookmarks working.
/// </summary>
public sealed class LegacyRootControllerRedirectMiddleware(RequestDelegate _next, LinkGenerator _linkGenerator)
{
    private static readonly HashSet<string> ControlledControllers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Network",
        "Clients",
        "Sector",
        "Receiver",
        "RequestsManagement"
    };

    public async Task InvokeAsync(HttpContext context)
    {
        // Only redirect authenticated traffic.
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            await _next(context);
            return;
        }

        var routeData = context.GetRouteData();
        var controller = routeData?.Values?["controller"]?.ToString();
        var action = routeData?.Values?["action"]?.ToString();
        var area = routeData?.Values?["area"]?.ToString();

        if (string.IsNullOrWhiteSpace(controller) || string.IsNullOrWhiteSpace(action))
        {
            await _next(context);
            return;
        }

        // Only handle legacy root (no area) controllers we control.
        if (!string.IsNullOrWhiteSpace(area) || !ControlledControllers.Contains(controller))
        {
            await _next(context);
            return;
        }

        var targetArea = ResolveTargetArea(context.User, controller);
        if (string.IsNullOrWhiteSpace(targetArea))
        {
            await _next(context);
            return;
        }

        // Build new route values preserving id (and any other route values).
        var newValues = new RouteValueDictionary(routeData!.Values);
        newValues["area"] = targetArea;
        newValues["controller"] = controller;
        newValues["action"] = action;

        var newPath = _linkGenerator.GetPathByAction(context, action, controller, newValues);
        if (string.IsNullOrWhiteSpace(newPath))
        {
            await _next(context);
            return;
        }

        // Avoid redirect loops.
        if (string.Equals(newPath, context.Request.Path.Value, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        context.Response.Redirect(newPath + context.Request.QueryString);
    }

    private static string? ResolveTargetArea(System.Security.Claims.ClaimsPrincipal user, string controller)
    {
        if (user.IsInRole(RoleNames.NetworkAdministrator))
        {
            return "CompanyAdmin";
        }

        if (user.IsInRole(RoleNames.CompanyEmployee) || user.IsInRole(RoleNames.EmployeeLegacy))
        {
            return "CompanyEmployee";
        }

        // SystemAdministrator legacy root Network page -> SystemAdmin Area.
        if (controller.Equals("Network", StringComparison.OrdinalIgnoreCase) &&
            user.IsInRole(RoleNames.SystemAdministrator))
        {
            return "SystemAdmin";
        }

        return null;
    }
}

