using RadaTik.Data;
using RadaTik.Services;

namespace RadaTik.Middleware;

/// <summary>يربط نطاق الشبكة بـ <see cref="ApplicationDbContext"/> لكل طلب HTTP.</summary>
public sealed class NetworkTenantMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        INetworkScopeResolver scopeResolver,
        ICurrentNetworkScope networkScope,
        ApplicationDbContext db)
    {
        if (ShouldResolveTenant(context))
        {
            try
            {
                // لا نربط بـ RequestAborted: فتح اتصال SQL قد يُلغى قبل اكتمال نطاق الأمان.
                await scopeResolver.ResolveAsync(context, CancellationToken.None);
            }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
            {
                throw;
            }
        }

        db.ApplyNetworkScope(networkScope);
        await next(context);
    }

    private static bool ShouldResolveTenant(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return false;
        }

        string path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/app", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/RadaTik", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/skyBeam", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Public", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.StartsWith("/Account/login", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Account/RegisterNetworkAdmin", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("/Account/forgotPassword", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }
}
