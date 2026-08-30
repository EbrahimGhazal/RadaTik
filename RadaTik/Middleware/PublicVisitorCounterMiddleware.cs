using RadaTik.Services.PublicStats;

namespace RadaTik.Middleware;

/// <summary>يعد الزائر الفريد لصفحات الموقع العامة مرة واحدة لكل متصفح.</summary>
public sealed class PublicVisitorCounterMiddleware(RequestDelegate next)
{
    public const string VisitorCookieName = "rt_site_visitor";

    public async Task InvokeAsync(HttpContext context, IPublicStatsService stats)
    {
        if (ShouldCount(context))
        {
            await stats.IncrementAsync(PublicStatsKeys.SiteVisitors, context.RequestAborted);
            context.Response.Cookies.Append(
                VisitorCookieName,
                "1",
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = context.Request.IsHttps,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddDays(400),
                });
        }

        await next(context);
    }

    internal static bool ShouldCount(HttpContext context)
    {
        if (!HttpMethods.IsGet(context.Request.Method))
        {
            return false;
        }

        if (context.Request.Cookies.ContainsKey(VisitorCookieName))
        {
            return false;
        }

        string path = context.Request.Path.Value ?? "/";
        if (!IsPublicSitePath(path))
        {
            return false;
        }

        if (IsLoopback(context))
        {
            return false;
        }

        string userAgent = context.Request.Headers.UserAgent.ToString();
        if (string.IsNullOrWhiteSpace(userAgent) || LooksLikeBot(userAgent))
        {
            return false;
        }

        return true;
    }

    public static bool IsPublicSitePath(string path)
    {
        if (path.Contains('.', StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Equals("/", StringComparison.Ordinal))
        {
            return true;
        }

        if (!path.StartsWith("/radatik", StringComparison.OrdinalIgnoreCase) &&
            !path.StartsWith("/RadaTik", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (path.Contains("/Download", StringComparison.OrdinalIgnoreCase) ||
            path.Contains("/Apps/android", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static bool IsLoopback(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is not null && System.Net.IPAddress.IsLoopback(ip);
    }

    private static bool LooksLikeBot(string userAgent)
    {
        return userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("spider", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("crawler", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("curl/", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("wget", StringComparison.OrdinalIgnoreCase);
    }
}
