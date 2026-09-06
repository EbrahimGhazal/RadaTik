using System.Collections.Concurrent;
using System.Net;
using RadaTik.Services.PublicStats;

namespace RadaTik.Middleware;

/// <summary>
/// يعد الزائر الفريد لصفحات الموقع العامة مرة واحدة لكل متصفح.
/// يمنع العدّ المضاعف الناتج عن Redirect من / إلى /RadaTik وطلبات المستند المتزامنة قبل تثبيت الكوكي.
/// </summary>
public sealed class PublicVisitorCounterMiddleware(RequestDelegate next)
{
    public const string VisitorCookieName = "rt_site_visitor";
    private static readonly TimeSpan DedupeWindow = TimeSpan.FromHours(24);

    public async Task InvokeAsync(HttpContext context, IPublicStatsService stats)
    {
        if (ShouldCount(context))
        {
            string dedupeKey = BuildDedupeKey(context);
            if (VisitorDedupeGate.TryMark(dedupeKey, DedupeWindow))
            {
                await stats.IncrementAsync(PublicStatsKeys.SiteVisitors, context.RequestAborted);
            }

            AppendVisitorCookie(context);
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

        if (!IsPrimaryDocumentRequest(context.Request))
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
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        // الجذر يعيد توجيه دائم إلى /RadaTik — عده يسبب +2 لكل زيارة أولى.
        if (path.Equals("/", StringComparison.Ordinal))
        {
            return false;
        }

        if (path.Contains('.', StringComparison.Ordinal))
        {
            return false;
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

    public static bool IsPrimaryDocumentRequest(HttpRequest request)
    {
        string dest = request.Headers["Sec-Fetch-Dest"].ToString();
        if (!string.IsNullOrWhiteSpace(dest) &&
            !dest.Equals("document", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string mode = request.Headers["Sec-Fetch-Mode"].ToString();
        if (!string.IsNullOrWhiteSpace(mode) &&
            !mode.Equals("navigate", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string accept = request.Headers.Accept.ToString();
        if (!string.IsNullOrWhiteSpace(accept) &&
            !accept.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            !accept.Contains("*/*", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static void AppendVisitorCookie(HttpContext context)
    {
        bool secure = context.Request.IsHttps ||
                      string.Equals(
                          context.Request.Headers["X-Forwarded-Proto"].ToString(),
                          "https",
                          StringComparison.OrdinalIgnoreCase);

        context.Response.Cookies.Append(
            VisitorCookieName,
            "1",
            new CookieOptions
            {
                HttpOnly = true,
                IsEssential = true,
                SameSite = SameSiteMode.Lax,
                Secure = secure,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(400),
            });
    }

    private static string BuildDedupeKey(HttpContext context)
    {
        string ip = context.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
                    ?? context.Request.Headers["X-Forwarded-For"].FirstOrDefault()?.Split(',')[0].Trim()
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "unknown";
        string ua = context.Request.Headers.UserAgent.ToString();
        return $"rt-visitor:{ip}:{ua.GetHashCode(StringComparison.Ordinal)}";
    }

    private static bool IsLoopback(HttpContext context)
    {
        var ip = context.Connection.RemoteIpAddress;
        return ip is not null && IPAddress.IsLoopback(ip);
    }

    private static bool LooksLikeBot(string userAgent)
    {
        return userAgent.Contains("bot", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("spider", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("crawler", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("preview", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("curl/", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("wget", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("python-requests", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("httpclient", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains("monitoring", StringComparison.OrdinalIgnoreCase);
    }

    private static class VisitorDedupeGate
    {
        private static readonly ConcurrentDictionary<string, long> Gate = new(StringComparer.Ordinal);

        public static bool TryMark(string key, TimeSpan ttl)
        {
            long expiresAt = DateTimeOffset.UtcNow.Add(ttl).ToUnixTimeSeconds();
            while (true)
            {
                if (Gate.TryAdd(key, expiresAt))
                {
                    return true;
                }

                if (!Gate.TryGetValue(key, out long existingExpiresAt))
                {
                    continue;
                }

                if (existingExpiresAt > DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                {
                    return false;
                }

                if (Gate.TryUpdate(key, expiresAt, existingExpiresAt))
                {
                    return true;
                }
            }
        }
    }
}
