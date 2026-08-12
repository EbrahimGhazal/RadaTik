using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace RadaTik.Controllers;

/// <summary>
/// إعادة توجيه المسارات القديمة (بدون prefix) إلى موقع SkyBeam.
/// </summary>
[AllowAnonymous]
public class PublicController : Controller
{
    [HttpGet]
    public IActionResult Index() => RedirectToActionPermanent("Index", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult About() => RedirectToActionPermanent("About", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult Services() => RedirectToActionPermanent("Services", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult Packages() => RedirectToActionPermanent("Packages", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult Contact() => RedirectToActionPermanent("Contact", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult JoinAsClient() => RedirectToActionPermanent("JoinAsClient", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult JoinAsEmployee() => RedirectToActionPermanent("JoinAsEmployee", "Public", new { area = "SkyBeam" });

    [HttpGet]
    public IActionResult JoinSuccess() => RedirectToActionPermanent("JoinSuccess", "Public", new { area = "SkyBeam" });
}
