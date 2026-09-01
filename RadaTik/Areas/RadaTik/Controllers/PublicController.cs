using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using global::RadaTik.Constants;
using global::RadaTik.Helpers;
using global::RadaTik.Services.PublicStats;
using global::RadaTik.ViewModels.Public;

namespace RadaTik.Areas.RadaTik.Controllers;

/// <summary>الصفحات التسويقية لمنصة RadaTik (B2B).</summary>
[Area("RadaTik")]
[AllowAnonymous]
public class PublicController : Controller
{
    public const string AndroidApkFileName = "radatik-client.apk";
    public const string AndroidApkDownloadName = "RadaTik-Client.apk";
    public const string CollectionApkFileName = "radatik-collection.apk";
    public const string CollectionApkDownloadName = "RadaTik-Collection.apk";
    public const string EmployeeApkFileName = "radatik-employee.apk";
    public const string EmployeeApkDownloadName = "RadaTik-Employee.apk";
    public const string CompanyApkFileName = "radatik-company.apk";
    public const string CompanyApkDownloadName = "RadaTik-Company.apk";

    private readonly ILogger<PublicController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly IPublicStatsService _publicStats;

    public PublicController(
        ILogger<PublicController> logger,
        IWebHostEnvironment environment,
        IPublicStatsService publicStats)
    {
        _logger = logger;
        _environment = environment;
        _publicStats = publicStats;
    }

    [HttpGet]
    public IActionResult Robots()
    {
        string body = PublicSeo.RobotsTxt(PublicSeo.PublicBaseUrl(Request));
        return Content(body, "text/plain; charset=utf-8");
    }

    [HttpGet]
    public IActionResult Sitemap()
    {
        string body = PublicSeo.SitemapXml(PublicSeo.PublicBaseUrl(Request), DateTimeOffset.UtcNow);
        return Content(body, "application/xml; charset=utf-8");
    }

    public IActionResult Index() => View();

    public IActionResult About() => View();

    public IActionResult Services() => View();

    public IActionResult Contact() => View();

    public async Task<IActionResult> Apps()
    {
        BindApkInfo("Client", AndroidApkFileName);
        BindApkInfo("Collection", CollectionApkFileName);
        BindApkInfo("Employee", EmployeeApkFileName);
        BindApkInfo("Company", CompanyApkFileName);
        ViewData["ClientDownloadCount"] = await _publicStats.GetAsync(PublicStatsKeys.ClientDownloads);
        ViewData["CollectionDownloadCount"] = await _publicStats.GetAsync(PublicStatsKeys.CollectionDownloads);
        ViewData["EmployeeDownloadCount"] = await _publicStats.GetAsync(PublicStatsKeys.EmployeeDownloads);
        ViewData["CompanyDownloadCount"] = await _publicStats.GetAsync(PublicStatsKeys.CompanyDownloads);
        return View();
    }

    [AcceptVerbs("GET", "HEAD")]
    public Task<IActionResult> DownloadAndroid() =>
        DownloadApk(AndroidApkFileName, AndroidApkDownloadName, PublicStatsKeys.ClientDownloads);

    [AcceptVerbs("GET", "HEAD")]
    public Task<IActionResult> DownloadCollection() =>
        DownloadApk(CollectionApkFileName, CollectionApkDownloadName, PublicStatsKeys.CollectionDownloads);

    [AcceptVerbs("GET", "HEAD")]
    public Task<IActionResult> DownloadEmployee() =>
        DownloadApk(EmployeeApkFileName, EmployeeApkDownloadName, PublicStatsKeys.EmployeeDownloads);

    [AcceptVerbs("GET", "HEAD")]
    public Task<IActionResult> DownloadCompany() =>
        DownloadApk(CompanyApkFileName, CompanyApkDownloadName, PublicStatsKeys.CompanyDownloads);

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

    private void BindApkInfo(string key, string fileName)
    {
        string apkPath = GetApkPath(fileName);
        bool available = System.IO.File.Exists(apkPath);
        ViewData[$"{key}ApkAvailable"] = available;
        if (!available)
        {
            return;
        }

        var info = new FileInfo(apkPath);
        ViewData[$"{key}ApkSizeMb"] = Math.Max(1, (int)Math.Ceiling(info.Length / (1024d * 1024d)));
        ViewData[$"{key}ApkUpdatedAt"] = info.LastWriteTime;
    }

    private async Task<IActionResult> DownloadApk(string fileName, string downloadName, string counterKey)
    {
        string apkPath = GetApkPath(fileName);
        if (!System.IO.File.Exists(apkPath))
        {
            TempData["Error"] = "ملف التطبيق غير متوفر حالياً. يرجى المحاولة لاحقاً.";
            return RedirectToAction(nameof(Apps));
        }

        if (HttpMethods.IsGet(Request.Method))
        {
            await _publicStats.IncrementAsync(counterKey);
        }

        return PhysicalFile(apkPath, "application/vnd.android.package-archive", downloadName);
    }

    private string GetApkPath(string fileName) =>
        Path.Combine(_environment.WebRootPath ?? "", "downloads", fileName);
}
