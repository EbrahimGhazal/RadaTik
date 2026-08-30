using Microsoft.AspNetCore.Http;

namespace RadaTik.Security;

/// <summary>تحديد تطبيق أندرويد/آيفون الحالي وتقييد تسجيل الدخول بدوره.</summary>
public static class NativeAppContext
{
    public const string CookieName = "rt_native_app";
    public const string QueryKey = "app";
    public const string Client = "client";
    public const string Collection = "collection";
    public const string Employee = "employee";
    public const string UserAgentMarker = "RadaTikNative/";

    public static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        string trimmed = value.Trim().ToLowerInvariant();
        return trimmed switch
        {
            Client or "subscriber" or "clientportal" => Client,
            Collection or "collectionpoint" or "collector" => Collection,
            Employee or "companyemployee" => Employee,
            _ => null,
        };
    }

    public static string? Detect(HttpRequest request, string? returnUrl = null)
    {
        string? fromQuery = Normalize(request.Query[QueryKey].ToString());
        if (fromQuery != null)
        {
            return fromQuery;
        }

        if (request.Cookies.TryGetValue(CookieName, out string? cookieValue))
        {
            string? fromCookie = Normalize(cookieValue);
            if (fromCookie != null)
            {
                return fromCookie;
            }
        }

        string userAgent = request.Headers.UserAgent.ToString();
        int marker = userAgent.IndexOf(UserAgentMarker, StringComparison.OrdinalIgnoreCase);
        if (marker >= 0)
        {
            string after = userAgent[(marker + UserAgentMarker.Length)..];
            string token = after.Split(' ', '/', ';')[0];
            string? fromAgent = Normalize(token);
            if (fromAgent != null)
            {
                return fromAgent;
            }
        }

        if (!LooksLikeNativeShell(userAgent))
        {
            return null;
        }

        return InferFromPath(returnUrl) ?? InferFromPath(request.Path.Value);
    }

    public static string? InferFromPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        string value = path.Trim();
        int query = value.IndexOf('?', StringComparison.Ordinal);
        if (query >= 0)
        {
            value = value[..query];
        }

        if (value.Contains("/clientPortal", StringComparison.OrdinalIgnoreCase))
        {
            return Client;
        }

        if (value.Contains("/collectionPoint", StringComparison.OrdinalIgnoreCase))
        {
            return Collection;
        }

        if (value.Contains("/employee", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/CompanyEmployee", StringComparison.OrdinalIgnoreCase))
        {
            return Employee;
        }

        return null;
    }

    public static bool LooksLikeNativeShell(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return false;
        }

        return userAgent.Contains("Capacitor", StringComparison.OrdinalIgnoreCase) ||
               userAgent.Contains(UserAgentMarker, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsRoleAllowed(string? app, IEnumerable<string> roles)
    {
        string? normalized = Normalize(app);
        if (normalized == null)
        {
            return true;
        }

        HashSet<string> set = roles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return normalized switch
        {
            Client => set.Contains(RoleNames.Client),
            Collection => set.Contains(RoleNames.CollectionPoint),
            Employee => set.Contains(RoleNames.CompanyEmployee) || set.Contains(RoleNames.EmployeeLegacy),
            _ => true,
        };
    }

    public static bool IsRoleAllowed(string? app, HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated != true)
        {
            return true;
        }

        string? normalized = Normalize(app);
        if (normalized == null)
        {
            return true;
        }

        return normalized switch
        {
            Client => context.User.IsInRole(RoleNames.Client),
            Collection => context.User.IsInRole(RoleNames.CollectionPoint),
            Employee => context.User.IsInRole(RoleNames.CompanyEmployee) || context.User.IsInRole(RoleNames.EmployeeLegacy),
            _ => true,
        };
    }

    public static string DisplayName(string? app) => Normalize(app) switch
    {
        Client => "تطبيق المشترك",
        Collection => "تطبيق التحصيل",
        Employee => "تطبيق الموظف",
        _ => "RadaTik",
    };

    public static string DeniedMessage(string? app) => Normalize(app) switch
    {
        Client => "هذا التطبيق مخصص للمشتركين فقط. سجّل الدخول بحساب المشترك، أو استخدم تطبيق دورك.",
        Collection => "هذا التطبيق مخصص لنقاط التحصيل فقط. سجّل الدخول بحساب نقطة التحصيل، أو استخدم تطبيق دورك.",
        Employee => "هذا التطبيق مخصص للموظفين فقط. سجّل الدخول بحساب الموظف، أو استخدم تطبيق دورك.",
        _ => "هذا الحساب لا يملك صلاحية الدخول إلى هذا التطبيق.",
    };

    public static void ApplyCookie(HttpResponse response, CookieOptions options, string? app)
    {
        string? normalized = Normalize(app);
        if (normalized == null)
        {
            return;
        }

        response.Cookies.Append(CookieName, normalized, options);
    }

    public static CookieOptions CreateCookieOptions(HttpRequest request)
    {
        return new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Secure = request.IsHttps,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddDays(400),
        };
    }
}
