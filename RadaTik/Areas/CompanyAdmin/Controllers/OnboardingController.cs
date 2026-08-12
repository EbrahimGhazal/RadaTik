using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class OnboardingController(
    UserManager<ApplicationUser> userManager,
    IOnboardingChecklistService onboardingService) : Controller
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss(CancellationToken cancellationToken)
    {
        ApplicationUser? user = await userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        await onboardingService.DismissCompanyAsync(user.Id, cancellationToken);
        TempData["Success"] = "تم إخفاء دليل البدء. يمكنك متابعة الإعداد من القائمة الجانبية في أي وقت.";
        return RedirectToRoute("networkManager-dashboard");
    }
}
