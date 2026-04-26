using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadTik.Security;

namespace RadTik.Areas.ClientPortal.Controllers;

[Area("ClientPortal")]
[Authorize(Roles = RoleNames.Client)]
public class DashboardController : Controller
{
    /// <summary>
    /// لوحة تحكم العميل (Client) - إعادة استخدام بوابة العميل الحالية.
    /// </summary>
    public IActionResult Index()
    {
        // Keep /clientPortal/dashboard working, but reuse the main ClientPortal controller page.
        // (The ClientPortalController loads the full model and renders the correct view.)
        return RedirectToRoute("clientPortal-actions", new { action = "Index" });
    }
}

