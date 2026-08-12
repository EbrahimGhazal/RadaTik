using Microsoft.AspNetCore.Identity;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Middleware;

/// <summary>إجبار المشترك على تغيير كلمة مرور البوابة عند أول دخول.</summary>
public sealed class ClientPortalSetupMiddleware(
    RequestDelegate next,
    ILogger<ClientPortalSetupMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
    {
        if (context.User?.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole(RoleNames.Client))
        {
           await next(context);
            return;
        }

        string path = context.Request.Path.Value ?? string.Empty;
        if (IsAlwaysAllowed(path))
        {
            await next(context);
            return;
        }

        if (!path.StartsWith("/clientPortal", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        ApplicationUser? user = await userManager.GetUserAsync(context.User);
        if (user == null || !user.MustChangePassword)
        {
            await next(context);
            return;
        }

        if (!IsPasswordSetupPath(path))
        {
            logger.LogInformation(
                "Redirecting client {UserName} to required portal password change.",
                user.UserName);
            context.Response.Redirect("/clientPortal/setup/requiredPassword");
            return;
        }

        await next(context);
    }

    private static bool IsAlwaysAllowed(string path) =>
        path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase);

    private static bool IsPasswordSetupPath(string path) =>
        path.StartsWith("/clientPortal/setup/requiredPassword", StringComparison.OrdinalIgnoreCase);
}
