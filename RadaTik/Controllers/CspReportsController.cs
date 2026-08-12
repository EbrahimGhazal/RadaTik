using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RadaTik.Controllers;

[ApiController]
[Route("api/security/csp-reports")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public sealed class CspReportsController : ControllerBase
{
    private readonly ILogger<CspReportsController> _logger;

    public CspReportsController(ILogger<CspReportsController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    [Consumes("application/csp-report", "application/reports+json", "application/json")]
    public IActionResult Receive([FromBody] JsonElement reportPayload)
    {
        var rawPayload = reportPayload.GetRawText();
        var truncatedPayload = rawPayload.Length <= 4000
            ? rawPayload
            : rawPayload[..4000] + "...(truncated)";

        _logger.LogWarning(
            "CSP report received. UserAgent={UserAgent}; Path={Path}; Payload={Payload}",
            Request.Headers.UserAgent.ToString(),
            HttpContext.Request.Path.Value ?? string.Empty,
            truncatedPayload);

        return NoContent();
    }
}
