using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.ViewModels.Public;

namespace RadaTik.Areas.RadaTik.Controllers;

/// <summary>الصفحات التسويقية لمنصة RadaTik (B2B).</summary>
[Area("RadaTik")]
[AllowAnonymous]
public class PublicController : Controller
{
    private readonly ILogger<PublicController> _logger;

    public PublicController(ILogger<PublicController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index() => View();

    public IActionResult About() => View();

    public IActionResult Services() => View();

    public IActionResult Contact() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(ContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _logger.LogInformation("رسالة RadaTik من: {Name} - {Email} - {Subject}", model.Name, model.Email, model.Subject);
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(Contact));
    }

    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            // خارج Area حتى لا يُفسَّر Home كـ action ضمن مسار RadaTik/{action}
            return RedirectToAction("Index", "Home", new { area = "" });
        }

        string targetReturnUrl = string.IsNullOrWhiteSpace(returnUrl) ? "/RadaTik" : returnUrl;
        return RedirectToRoute("reglog-login", new { returnUrl = targetReturnUrl });
    }
}
