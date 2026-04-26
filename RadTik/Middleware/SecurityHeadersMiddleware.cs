namespace RadTik.Middleware;

/// <summary>
/// Adds baseline security headers for all HTTP responses.
/// Keep values conservative to avoid breaking existing behavior.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IWebHostEnvironment _environment;
    private readonly bool _enforceCsp;
    private readonly bool _enableReportOnly;
    private readonly string _cspReportEndpoint;
    private const string CspReportGroupName = "csp-endpoint";

    public SecurityHeadersMiddleware(RequestDelegate next, IWebHostEnvironment environment, IConfiguration configuration)
    {
        _next = next;
        _environment = environment;
        _enforceCsp = configuration.GetValue<bool>("Security:Csp:Enforce");
        _enableReportOnly = configuration.GetValue("Security:Csp:EnableReportOnly", true);
        _cspReportEndpoint = configuration.GetValue<string>("Security:Csp:ReportEndpoint")
            ?? "/api/security/csp-reports";
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // Prevent MIME sniffing
            headers.TryAdd("X-Content-Type-Options", "nosniff");

            // Mitigate clickjacking while still allowing same-origin frames if needed
            headers.TryAdd("X-Frame-Options", "SAMEORIGIN");

            // Avoid leaking full URLs to external sites
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

            // Disable powerful browser features by default
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

            var reportToHeader = $$"""
                {"group":"{{CspReportGroupName}}","max_age":10886400,"endpoints":[{"url":"{{_cspReportEndpoint}}"}]}
                """;
            headers.TryAdd("Report-To", reportToHeader);

            // Start CSP safely in report-only mode, then enable enforced mode via config.
            var cspPolicy =
                "default-src 'self'; " +
                "base-uri 'self'; " +
                "object-src 'none'; " +
                "frame-ancestors 'self'; " +
                "form-action 'self'; " +
                "img-src 'self' data: blob:; " +
                "style-src 'self' 'unsafe-inline'; " +
                "script-src 'self' 'unsafe-inline' 'unsafe-eval'; " +
                "font-src 'self' data:; " +
                "connect-src 'self' ws: wss:; " +
                $"report-uri {_cspReportEndpoint}; " +
                $"report-to {CspReportGroupName};";

            if (_enableReportOnly)
            {
                headers.TryAdd("Content-Security-Policy-Report-Only", cspPolicy);
            }

            if (_enforceCsp)
            {
                headers.TryAdd("Content-Security-Policy", cspPolicy);
            }

            // Enforce HTTPS for compatible clients in production.
            if (!_environment.IsDevelopment())
            {
                headers.TryAdd("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
            }

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
