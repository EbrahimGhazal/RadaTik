using Microsoft.AspNetCore.Http;

namespace RadaTik.Helpers;

/// <summary>بيانات SEO لصفحات RadaTik العامة فقط.</summary>
public static class PublicSeo
{
    public const string Brand = "RadaTik";
    public const string DefaultTitle = "نظام إدارة مزودي خدمة الإنترنت";
    public const string DefaultDescription =
        "RadaTik منصة عربية لإدارة مزودي خدمة الإنترنت: المشتركين، MikroTik، الفوترة، نقاط التحصيل، المخازن، والرواتب في نظام واحد.";

    public static string DescriptionFor(string? action) => action switch
    {
        "About" => "تعرّف على RadaTik: منصة لإدارة شركات الإنترنت من التشغيل اليومي حتى التقارير والمحاسبة.",
        "Services" => "خدمات RadaTik لإدارة المشتركين، MikroTik، المحافظ، التحصيل، المخازن، والرواتب لمزودي خدمة الإنترنت.",
        "Apps" => "حمّل تطبيقات RadaTik للمشترك ونقطة التحصيل والموظف ومدير الشركة لإدارة الخدمة من الهاتف.",
        "Contact" => "تواصل مع فريق RadaTik لتسجيل شركتك أو طلب عرض لنظام إدارة مزود خدمة الإنترنت.",
        _ => DefaultDescription,
    };

    public static string PublicBaseUrl(HttpRequest request)
    {
        string host = FirstForwardedHost(request) ?? request.Host.Host;
        if (host.Equals("radatik.com", StringComparison.OrdinalIgnoreCase) ||
            host.Equals("www.radatik.com", StringComparison.OrdinalIgnoreCase))
        {
            return "https://radatik.com";
        }

        string scheme = request.IsHttps ? "https" : request.Scheme;
        return $"{scheme}://{request.Host.Value}".TrimEnd('/');
    }

    private static string? FirstForwardedHost(HttpRequest request)
    {
        string? raw = request.Headers["X-Forwarded-Host"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        string host = raw.Split(',')[0].Trim();
        int colon = host.IndexOf(':');
        return colon > 0 ? host[..colon] : host;
    }

    public static string CanonicalPath(string? action) => action switch
    {
        "About" => "/RadaTik/About",
        "Services" => "/RadaTik/Services",
        "Apps" => "/RadaTik/Apps",
        "Contact" => "/RadaTik/Contact",
        _ => "/RadaTik",
    };

    public static string RobotsTxt(string baseUrl) =>
        $"""
        User-agent: *
        Allow: /RadaTik
        Allow: /RadaTik/
        Allow: /css/
        Allow: /images/
        Allow: /js/
        Disallow: /Account
        Disallow: /networkManager
        Disallow: /employee
        Disallow: /clientPortal
        Disallow: /collectionPoint
        Disallow: /systemAdmin
        Disallow: /CompanyAdmin
        Disallow: /CompanyEmployee
        Disallow: /app
        Disallow: /downloads

        Sitemap: {baseUrl.TrimEnd('/')}/sitemap.xml
        """;

    public static string SitemapXml(string baseUrl, DateTimeOffset lastModified)
    {
        string stamp = lastModified.UtcDateTime.ToString("yyyy-MM-dd");
        string root = baseUrl.TrimEnd('/');
        string[] paths = ["/RadaTik", "/RadaTik/About", "/RadaTik/Services", "/RadaTik/Apps", "/RadaTik/Contact"];
        var body = string.Join("", paths.Select(path =>
            $"""
              <url>
                <loc>{root}{path}</loc>
                <lastmod>{stamp}</lastmod>
                <changefreq>weekly</changefreq>
              </url>
            """));
        return
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">
            {body}
            </urlset>
            """;
    }
}
