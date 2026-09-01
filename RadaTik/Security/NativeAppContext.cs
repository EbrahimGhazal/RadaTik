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
    public const string Company = "company";
    public const string UserAgentMarker = "RadaTikNative/";
    public const int CurrentVersion = 2;
    /// <summary>
    /// 0 = لا نغلق التطبيقات المثبّتة. الإغلاق على 2 منع كل النسخ الحالية لأنها لا ترسل رقم إصدار.
    /// </summary>
    public const int MinimumSupportedVersion = 0;

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
            Company or "companyadmin" or "networkadmin" or "networkmanager" or "networkadministrator" => Company,
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
        string? fromAgent = ReadRoleFromUserAgent(userAgent);
        if (fromAgent != null)
        {
            return fromAgent;
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

        if (value.Contains("/networkManager", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("/CompanyAdmin", StringComparison.OrdinalIgnoreCase))
        {
            return Company;
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
            Company => set.Contains(RoleNames.NetworkAdministrator),
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
            Company => context.User.IsInRole(RoleNames.NetworkAdministrator),
            _ => true,
        };
    }

    public static string DisplayName(string? app) => Normalize(app) switch
    {
        Client => "تطبيق المشترك",
        Collection => "تطبيق التحصيل",
        Employee => "تطبيق الموظف",
        Company => "تطبيق مدير الشركة",
        _ => "RadaTik",
    };

    public static string DeniedMessage(string? app) => Normalize(app) switch
    {
        Client => "هذا التطبيق مخصص للمشتركين فقط. سجّل الدخول بحساب المشترك، أو استخدم تطبيق دورك.",
        Collection => "هذا التطبيق مخصص لنقاط التحصيل فقط. سجّل الدخول بحساب نقطة التحصيل، أو استخدم تطبيق دورك.",
        Employee => "هذا التطبيق مخصص للموظفين فقط. سجّل الدخول بحساب الموظف، أو استخدم تطبيق دورك.",
        Company => "هذا التطبيق مخصص لمديري الشركات فقط. سجّل الدخول بحساب مدير الشركة، أو استخدم تطبيق دورك.",
        _ => "هذا الحساب لا يملك صلاحية الدخول إلى هذا التطبيق.",
    };

    public static string? ReadRoleFromUserAgent(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return null;
        }

        int marker = userAgent.IndexOf(UserAgentMarker, StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return null;
        }

        string after = userAgent[(marker + UserAgentMarker.Length)..];
        string token = after.Split(' ', '/', ';')[0];
        return Normalize(token);
    }

    public static int ReadVersion(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return 0;
        }

        int marker = userAgent.IndexOf(UserAgentMarker, StringComparison.OrdinalIgnoreCase);
        if (marker < 0)
        {
            return 0;
        }

        string after = userAgent[(marker + UserAgentMarker.Length)..];
        string[] parts = after.Split([' ', '/', ';'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2 && int.TryParse(parts[1], out int version) && version > 0)
        {
            return version;
        }

        return 0;
    }

    public static bool IsNativeAppOutdated(string? userAgent)
    {
        if (!LooksLikeNativeShell(userAgent))
        {
            return false;
        }

        return ReadVersion(userAgent) < MinimumSupportedVersion;
    }

    public static bool IsVersionGateExempt(PathString path)
    {
        string value = path.Value ?? string.Empty;
        return value.StartsWith("/Account/AppUpdateRequired", StringComparison.OrdinalIgnoreCase) ||
               value.Equals("/Account/logout", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("/RadaTik/Apps", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("/RadaTik/Download", StringComparison.OrdinalIgnoreCase) ||
               value.StartsWith("/downloads/", StringComparison.OrdinalIgnoreCase);
    }

    public static string DownloadPath(string? app) => Normalize(app) switch
    {
        Client => "/RadaTik/DownloadAndroid",
        Collection => "/RadaTik/DownloadCollection",
        Employee => "/RadaTik/DownloadEmployee",
        Company => "/RadaTik/DownloadCompany",
        _ => "/RadaTik/Apps",
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
