using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Services;
using global::RadaTik.ViewModels.Public;

namespace RadaTik.Areas.SkyBeam.Controllers;

/// <summary>الصفحات العامة لموقع SkyBeam (ISP).</summary>
[Area("SkyBeam")]
[AllowAnonymous]
public class PublicController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PublicController> _logger;
    private readonly IRequestNotificationService _requestNotificationService;

    public PublicController(
        ApplicationDbContext context,
        ILogger<PublicController> logger,
        IRequestNotificationService requestNotificationService)
    {
        _context = context;
        _logger = logger;
        _requestNotificationService = requestNotificationService;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.Profiles = await GetActiveClientProfilesAsync(take: 6);
        (int Clients, int Sectors, int Receivers) stats = await GetLandingStatsSafeAsync();
        ViewBag.TotalClients = stats.Clients;
        ViewBag.TotalSectors = stats.Sectors;
        ViewBag.TotalReceivers = stats.Receivers;

        return View();
    }

    public IActionResult About() => View();

    public IActionResult Services() => View();

    public async Task<IActionResult> Packages()
    {
        List<Profile> profiles = await GetActiveClientProfilesAsync();
        return View(profiles);
    }

    public IActionResult Contact() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Contact(ContactViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _logger.LogInformation("تم استلام رسالة SkyBeam من: {Name} - {Email}", model.Name, model.Email);
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(Contact));
    }

    public async Task<IActionResult> JoinAsClient()
    {
        ViewBag.Profiles = await GetActiveClientProfilesAsync();
        return View(new JoinRequest { RequestType = JoinRequestType.Client });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> JoinAsClient(JoinRequest model)
    {
        model.RequestType = JoinRequestType.Client;
        ModelState.Remove("Qualification");
        ModelState.Remove("Experience");
        ModelState.Remove("DesiredPosition");

        if (!ModelState.IsValid)
        {
            ViewBag.Profiles = await GetActiveClientProfilesAsync();
            return View(model);
        }

        bool existingRequest = await _context.JoinRequests
            .AnyAsync(j => j.Email == model.Email && j.Status == JoinRequestStatus.Pending);

        if (existingRequest)
        {
            ModelState.AddModelError("", "يوجد طلب سابق بهذا البريد الإلكتروني قيد المراجعة");
            ViewBag.Profiles = await GetActiveClientProfilesAsync();
            return View(model);
        }

        model.CreatedDate = DateTime.Now;
        model.Status = JoinRequestStatus.Pending;

        _context.JoinRequests.Add(model);
        await _context.SaveChangesAsync();
        await _requestNotificationService.NotifyClientJoinRequestSubmittedAsync(model);

        _logger.LogInformation("طلب انضمام SkyBeam كعميل: {Name} - {Email}", model.FullName, model.Email);
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(JoinSuccess));
    }

    public IActionResult JoinAsEmployee()
    {
        return View(new JoinRequest { RequestType = JoinRequestType.Employee });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> JoinAsEmployee(JoinRequest model)
    {
        model.RequestType = JoinRequestType.Employee;
        ModelState.Remove("NationalId");
        ModelState.Remove("RequestedProfileId");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        bool existingRequest = await _context.JoinRequests
            .AnyAsync(j => j.Email == model.Email && j.Status == JoinRequestStatus.Pending);

        if (existingRequest)
        {
            ModelState.AddModelError("", "يوجد طلب سابق بهذا البريد الإلكتروني قيد المراجعة");
            return View(model);
        }

        model.CreatedDate = DateTime.Now;
        model.Status = JoinRequestStatus.Pending;

        _context.JoinRequests.Add(model);
        await _context.SaveChangesAsync();
        await _requestNotificationService.NotifyEmployeeJoinRequestSubmittedAsync(model);

        _logger.LogInformation("طلب انضمام SkyBeam كموظف: {Name} - {Email}", model.FullName, model.Email);
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(JoinSuccess));
    }

    public IActionResult JoinSuccess() => View();

    private async Task<List<Profile>> GetActiveClientProfilesAsync(int? take = null)
    {
        try
        {
            IOrderedQueryable<Profile> q = _context.Profiles
                .Where(p => p.IsActive && p.IsForNewClients)
                .OrderBy(p => p.DisplayOrder)
                .ThenBy(p => p.Price);
            return take.HasValue
                ? await q.Take(take.Value).ToListAsync()
                : await q.ToListAsync();
        }
        catch (Exception ex) when (IsMissingSchemaSql(ex))
        {
            _logger.LogWarning(ex, "تعذر قراءة جدول Profiles للصفحة العامة SkyBeam.");
            return [];
        }
    }

    private async Task<(int Clients, int Sectors, int Receivers)> GetLandingStatsSafeAsync()
    {
        try
        {
            int clients = await _context.Clients.CountAsync();
            int sectors = await _context.Sectors.CountAsync(s => s.IsActive);
            int receivers = await _context.Receivers.CountAsync(r => r.IsActive);
            return (clients, sectors, receivers);
        }
        catch (Exception ex) when (IsMissingSchemaSql(ex))
        {
            _logger.LogWarning(ex, "تعذر قراءة إحصائيات SkyBeam.");
            return (0, 0, 0);
        }
    }

    private static bool IsMissingSchemaSql(Exception ex)
    {
        for (Exception e = ex; e != null; e = e.InnerException!)
        {
            if (e is SqlException sql && sql.Number == 208)
            {
                return true;
            }
        }

        return false;
    }
}
