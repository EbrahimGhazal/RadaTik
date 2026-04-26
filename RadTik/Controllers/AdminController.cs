using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RadTik.Security;
using RadTik.ViewModels.Admin;

namespace RadTik.Controllers;

/// <summary>
/// مسار قديم للتوافق: /Admin/*
/// المنطق الفعلي تم نقله إلى Area منظمة: `CompanyAdmin` داخل
/// `Areas/CompanyAdmin/Controllers/AdminController.cs`.
/// </summary>
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Index(string? q = null, string? type = null)
        => RedirectToAction("Index", "Admin", new { area = "CompanyAdmin", q, type });

    [HttpGet]
    public IActionResult CreateEmployee(string? returnUrl = null)
        => RedirectToAction("CreateEmployee", "Admin", new { area = "CompanyAdmin", returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult CreateEmployee(CreateEmployeeViewModel model)
        => RedirectToAction("CreateEmployee", "Admin", new { area = "CompanyAdmin" });

    [HttpGet]
    public IActionResult EditEmployee(string id, string? returnUrl = null)
        => RedirectToAction("EditEmployee", "Admin", new { area = "CompanyAdmin", id, returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditEmployee(EditEmployeeViewModel model)
        => RedirectToAction("EditEmployee", "Admin", new { area = "CompanyAdmin" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ToggleEmployeeStatus(string id, string? returnUrl = null)
        => RedirectToAction("ToggleEmployeeStatus", "Admin", new { area = "CompanyAdmin", id, returnUrl });

    [HttpGet]
    public IActionResult DeleteEmployee(string id, string? returnUrl = null)
        => RedirectToAction("DeleteEmployee", "Admin", new { area = "CompanyAdmin", id, returnUrl });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteEmployeeConfirmed(DeleteEmployeeViewModel model)
        => RedirectToAction("DeleteEmployeeConfirmed", "Admin", new { area = "CompanyAdmin" });
}

