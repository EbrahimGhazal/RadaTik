using Microsoft.AspNetCore.Identity;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;

namespace RadaTik.Middleware;

/// <summary>
/// يفرض على مدير النظام: (1) تغيير كلمة المرور عند أول دخول، (2) إكمال تهيئة التسعير.
/// </summary>
public sealed class SystemAdminSetupMiddleware(
    RequestDelegate next,
    ILogger<SystemAdminSetupMiddleware> logger)
{
    public async Task InvokeAsync(
        HttpContext context,
        UserManager<ApplicationUser> userManager,
        ISystemAdminPricingReadinessService pricingReadiness)
    {
        if (context.User?.Identity?.IsAuthenticated != true ||
            !context.User.IsInRole(RoleNames.SystemAdministrator))
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

        if (!path.StartsWith("/systemAdmin", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        ApplicationUser? user = await userManager.GetUserAsync(context.User);
        if (user == null)
        {
            await next(context);
            return;
        }

        if (user.MustChangePassword)
        {
            if (!IsPasswordSetupPath(path))
            {
                logger.LogInformation(
                    "Redirecting system admin {UserName} to required password change.",
                    user.UserName);
                context.Response.Redirect("/systemAdmin/setup/requiredPassword");
                return;
            }

            await next(context);
            return;
        }

        SystemAdminPricingReadiness pricing;
        try
        {
            // لا نربط بـ RequestAborted: فتح اتصال SQL قد يُلغى عند إيقاف التطبيق أو مغادرة الصفحة.
            pricing = await pricingReadiness.EvaluateAsync(CancellationToken.None);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            return;
        }

        if (!pricing.IsComplete && !IsPricingSetupAllowedPath(path))
        {
            logger.LogInformation(
                "Redirecting system admin {UserName} to pricing setup ({MissingCount} items missing).",
                user.UserName,
                pricing.MissingItems.Count);
            context.Response.Redirect("/systemAdmin/setup/pricing");
            return;
        }

        await next(context);
    }

    private static bool IsAlwaysAllowed(string path) =>
        path.StartsWith("/Account/", StringComparison.OrdinalIgnoreCase);

    private static bool IsPasswordSetupPath(string path) =>
        path.StartsWith("/systemAdmin/setup/requiredPassword", StringComparison.OrdinalIgnoreCase);

    private static bool IsPricingSetupAllowedPath(string path) =>
        path.StartsWith("/systemAdmin/setup/pricing", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/systemAdmin/serviceCatalog", StringComparison.OrdinalIgnoreCase);
}
