using Microsoft.AspNetCore.Identity;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Middleware;

/// <summary>يثبّت دور التطبيق الأصلي ويمنع جلسة دور آخر داخل نفس التطبيق.</summary>
public sealed class NativeAppRoleMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SignInManager<ApplicationUser> signInManager)
    {
        string? returnUrl = context.Request.Query["ReturnUrl"].ToString();
        if (string.IsNullOrWhiteSpace(returnUrl))
        {
            returnUrl = context.Request.Query["returnUrl"].ToString();
        }

        string? app = NativeAppContext.Detect(context.Request, returnUrl);
        if (app != null)
        {
            NativeAppContext.ApplyCookie(context.Response, NativeAppContext.CreateCookieOptions(context.Request), app);
            context.Items[NativeAppContext.QueryKey] = app;
        }

        if (app != null &&
            context.User?.Identity?.IsAuthenticated == true &&
            !NativeAppContext.IsRoleAllowed(app, context))
        {
            await signInManager.SignOutAsync();
            context.Session.Remove(AreaIsolationMiddleware.SessionKeyActiveArea);
            context.Response.Redirect($"/Account/login?{NativeAppContext.QueryKey}={Uri.EscapeDataString(app)}");
            return;
        }

        await next(context);
    }
}
