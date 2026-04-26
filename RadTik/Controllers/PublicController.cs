using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Services;
using RadTik.ViewModels.Public;

namespace RadTik.Controllers
{
    /// <summary>
    /// Controller للصفحات العامة (Landing Page)
    /// </summary>
    [AllowAnonymous]
    public class PublicController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PublicController> _logger;
        private readonly RequestNotificationService _requestNotificationService;

        public PublicController(
            ApplicationDbContext context,
            ILogger<PublicController> logger,
            RequestNotificationService requestNotificationService)
        {
            _context = context;
            _logger = logger;
            _requestNotificationService = requestNotificationService;
        }

        /// <summary>
        /// الصفحة الرئيسية العامة
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // إذا كان المستخدم مسجل دخول، توجيهه للوحة التحكم
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewBag.Profiles = await GetActiveClientProfilesAsync(take: 6);
            var stats = await GetLandingStatsSafeAsync();
            ViewBag.TotalClients = stats.Clients;
            ViewBag.TotalSectors = stats.Sectors;
            ViewBag.TotalReceivers = stats.Receivers;

            return View();
        }

        /// <summary>
        /// صفحة من نحن
        /// </summary>
        public IActionResult About()
        {
            return View();
        }

        /// <summary>
        /// صفحة الخدمات
        /// </summary>
        public IActionResult Services()
        {
            return View();
        }

        /// <summary>
        /// صفحة الباقات والأسعار
        /// </summary>
        public async Task<IActionResult> Packages()
        {
            var profiles = await GetActiveClientProfilesAsync();
            return View(profiles);
        }

        /// <summary>
        /// صفحة اتصل بنا
        /// </summary>
        public IActionResult Contact()
        {
            return View();
        }

        /// <summary>
        /// معالجة نموذج اتصل بنا
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Contact(ContactViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // يمكن إضافة منطق إرسال البريد الإلكتروني هنا
            _logger.LogInformation($"تم استلام رسالة من: {model.Name} - {model.Email}");

            TempData["Success"] = "شكراً لتواصلك معنا! سنرد عليك في أقرب وقت ممكن.";
            return RedirectToAction(nameof(Contact));
        }

        /// <summary>
        /// صفحة طلب الانضمام كعميل
        /// </summary>
        public async Task<IActionResult> JoinAsClient()
        {
            ViewBag.Profiles = await GetActiveClientProfilesAsync();
            return View(new JoinRequest { RequestType = JoinRequestType.Client });
        }

        /// <summary>
        /// معالجة طلب الانضمام كعميل
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinAsClient(JoinRequest model)
        {
            model.RequestType = JoinRequestType.Client;

            // إزالة validation للحقول الخاصة بالموظفين
            ModelState.Remove("Qualification");
            ModelState.Remove("Experience");
            ModelState.Remove("DesiredPosition");

            if (!ModelState.IsValid)
            {
                ViewBag.Profiles = await GetActiveClientProfilesAsync();
                return View(model);
            }

            // التحقق من عدم وجود طلب سابق بنفس البريد
            var existingRequest = await _context.JoinRequests
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

            _logger.LogInformation($"تم تقديم طلب انضمام جديد كعميل: {model.FullName} - {model.Email}");

            TempData["Success"] = "تم تقديم طلبك بنجاح! سيتم مراجعته والتواصل معك قريباً.";
            return RedirectToAction(nameof(JoinSuccess));
        }

        /// <summary>
        /// صفحة طلب الانضمام كموظف
        /// </summary>
        public IActionResult JoinAsEmployee()
        {
            return View(new JoinRequest { RequestType = JoinRequestType.Employee });
        }

        /// <summary>
        /// معالجة طلب الانضمام كموظف
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinAsEmployee(JoinRequest model)
        {
            model.RequestType = JoinRequestType.Employee;

            // إزالة validation للحقول الخاصة بالعملاء
            ModelState.Remove("NationalId");
            ModelState.Remove("RequestedProfileId");

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // التحقق من عدم وجود طلب سابق
            var existingRequest = await _context.JoinRequests
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

            _logger.LogInformation($"تم تقديم طلب انضمام جديد كموظف: {model.FullName} - {model.Email}");

            TempData["Success"] = "تم تقديم طلبك بنجاح! سيتم مراجعته والتواصل معك قريباً.";
            return RedirectToAction(nameof(JoinSuccess));
        }

        /// <summary>
        /// صفحة نجاح تقديم الطلب
        /// </summary>
        public IActionResult JoinSuccess()
        {
            return View();
        }

        /// <summary>
        /// باقات العرض العام — يتجنب إسقاط الصفحة إذا لم تُطبَّق الهجرات بعد (جدول Profiles غير موجود).
        /// </summary>
        private async Task<List<Profile>> GetActiveClientProfilesAsync(int? take = null)
        {
            try
            {
                var q = _context.Profiles
                    .Where(p => p.IsActive && p.IsForNewClients)
                    .OrderBy(p => p.DisplayOrder)
                    .ThenBy(p => p.Price);
                return take.HasValue
                    ? await q.Take(take.Value).ToListAsync()
                    : await q.ToListAsync();
            }
            catch (Exception ex) when (IsMissingSchemaSql(ex))
            {
                _logger.LogWarning(
                    ex,
                    "تعذر قراءة جدول Profiles. نفّذ: dotnet ef database update — الرسالة: {Message}",
                    ex.Message);
                return new List<Profile>();
            }
        }

        private async Task<(int Clients, int Sectors, int Receivers)> GetLandingStatsSafeAsync()
        {
            try
            {
                var clients = await _context.Clients.CountAsync();
                var sectors = await _context.Sectors.CountAsync(s => s.IsActive);
                var receivers = await _context.Receivers.CountAsync(r => r.IsActive);
                return (clients, sectors, receivers);
            }
            catch (Exception ex) when (IsMissingSchemaSql(ex))
            {
                _logger.LogWarning(
                    ex,
                    "تعذر قراءة إحصائيات الصفحة الرئيسية (جداول غير جاهزة). نفّذ الهجرات. {Message}",
                    ex.Message);
                return (0, 0, 0);
            }
        }

        /// <summary>خطأ SQL 208 = اسم كائن غير صالح (جدول غير موجود عادةً قبل تطبيق الهجرات).</summary>
        private static bool IsMissingSchemaSql(Exception ex)
        {
            for (var e = ex; e != null; e = e.InnerException!)
            {
                if (e is SqlException sql && sql.Number == 208)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
