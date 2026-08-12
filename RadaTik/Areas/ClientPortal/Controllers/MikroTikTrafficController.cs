using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Security;

namespace RadaTik.Areas.ClientPortal.Controllers;

/// <summary>
/// مراقبة ترافك MikroTik لبوابة العميل — خادم الحساب وملف العميل يُحدَّدان تلقائياً.
/// </summary>
[Area("ClientPortal")]
[Authorize(Roles = RoleNames.Client)]
public sealed class MikroTikTrafficController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return RedirectToRoute("clientPortal-actions", new { action = "MyTraffic" });
    }
}
