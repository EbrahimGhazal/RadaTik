using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.Reports;
using RadTik.ViewModels.CompanyAdmin;
using System.Text;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Reports)]
public sealed class ReportsController : Controller
{
    private const string DefaultTemplateSample = """
<div style="text-align:center;direction:rtl">
  <h2>{{ReportTitle}}</h2>
  <p>الشركة: <strong>{{CompanyName}}</strong> — الشبكة المحددة: <strong>{{NetworkName}}</strong></p>
  <p>الفترة: من {{PeriodFrom}} إلى {{PeriodTo}} — عدد السجلات: {{RowCount}}</p>
</div>
{{DATA_TABLE}}
<p style="font-size:12px;color:#666;text-align:center">أُعدّ في {{GeneratedAt}} — {{ManagerName}}</p>
""";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUsageBasedSubscriptionChargeService _usageCharge;

    public ReportsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageCharge)
    {
        _db = db;
        _userManager = userManager;
        _usageCharge = usageCharge;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "التقارير";

        var user = await _userManager.GetUserAsync(User);
        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToRoute("networkManager-network");
        }

        var selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        var effectiveCompanyId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;

        var exportPrice = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p => p.IsActive && p.FeatureKey == FeatureKeys.ReportsExport)
            .OrderByDescending(p => p.Id)
            .Select(p => p.AmountSYP)
            .FirstOrDefaultAsync();

        ViewBag.ExportPriceSyp = exportPrice;
        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_db, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;
        ViewBag.EffectiveCompanyNetworkId = effectiveCompanyId;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Templates()
    {
        ViewData["Title"] = "قوالب التقارير";

        var user = await _userManager.GetUserAsync(User);
        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToRoute("networkManager-network");
        }

        var selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;

        var existing = await _db.NetworkReportTemplates
            .AsNoTracking()
            .Where(t => t.CompanyNetworkId == companyNetworkId)
            .ToDictionaryAsync(t => t.ReportKind, t => t.UpdatedAt);

        ViewBag.ExistingKinds = existing;
        ViewBag.CompanyNetworkId = companyNetworkId;
        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_db, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> EditTemplate(CompanyReportKind kind)
    {
        if (kind is < CompanyReportKind.Subscribers or > CompanyReportKind.Subcontractors)
        {
            return NotFound();
        }

        ViewData["Title"] = "تعديل قالب التقرير";

        var user = await _userManager.GetUserAsync(User);
        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToRoute("networkManager-network");
        }

        var selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;

        var row = await _db.NetworkReportTemplates
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == kind);

        ViewBag.Kind = (int)kind;
        ViewBag.KindTitle = ReportTemplateFormatter.GetReportTitleDisplay(kind);
        ViewBag.BodyContent = row?.BodyContent ?? "";
        ViewBag.DefaultSample = DefaultTemplateSample;
        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_db, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        return View();
    }

    public sealed class SaveTemplateForm
    {
        public CompanyReportKind Kind { get; set; }
        public string? BodyContent { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(SaveTemplateForm form)
    {
        if (form.Kind is < CompanyReportKind.Subscribers or > CompanyReportKind.Subcontractors)
        {
            TempData["Error"] = "نوع التقرير غير صالح.";
            return RedirectToAction(nameof(Templates));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً";
            return RedirectToRoute("networkManager-network");
        }

        var selectedNetwork = await _db.Networks.FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return RedirectToAction(nameof(Templates));
        }

        var companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;

        var body = string.IsNullOrWhiteSpace(form.BodyContent) ? null : form.BodyContent.Trim();
        var row = await _db.NetworkReportTemplates
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == form.Kind);

        var now = DateTime.UtcNow;
        if (row == null)
        {
            _db.NetworkReportTemplates.Add(new NetworkReportTemplate
            {
                CompanyNetworkId = companyNetworkId,
                ReportKind = form.Kind,
                BodyContent = body,
                UpdatedAt = now,
                UpdatedByUserId = user.Id
            });
        }
        else
        {
            row.BodyContent = body;
            row.UpdatedAt = now;
            row.UpdatedByUserId = user.Id;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = "تم حفظ القالب.";
        return RedirectToAction(nameof(EditTemplate), new { kind = form.Kind });
    }

    public sealed class ReportRunForm
    {
        public CompanyReportKind Kind { get; set; }
        public CompanyReportPeriodPreset Period { get; set; }
        public DateTime? CustomFrom { get; set; }
        public DateTime? CustomTo { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Print(ReportRunForm form)
    {
        var result = await BuildReportAsync(form, HttpContext.RequestAborted);
        if (result.Error != null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        return View("Result", result.ViewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportExcel(ReportRunForm form)
    {
        var result = await BuildReportAsync(form, HttpContext.RequestAborted);
        if (result.Error != null)
        {
            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        var vm = result.ViewModel!;
        var safeName = SanitizeFileName($"{vm.Title}_{vm.Range.FromInclusive:yyyyMMdd}_{vm.Range.ToInclusive:yyyyMMdd}.xlsx");
        var bytes = BuildExcelBytes(vm);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", safeName);
    }

    private sealed record BuildReportOutcome(CompanyReportsResultViewModel? ViewModel, string? Error);

    private async Task<BuildReportOutcome> BuildReportAsync(ReportRunForm form, CancellationToken ct)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new BuildReportOutcome(null, "يجب تسجيل الدخول.");
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            return new BuildReportOutcome(null, "يرجى تحديد شبكة أولاً.");
        }

        var selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value, ct);
        if (selectedNetwork == null)
        {
            return new BuildReportOutcome(null, "تعذر العثور على الشبكة.");
        }

        var companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        var networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);

        var range = CompanyReportDateRange.Resolve(form.Period, form.CustomFrom, form.CustomTo);
        var reportLabel = $"{ReportTemplateFormatter.GetReportTitleDisplay(form.Kind)} — {range.FromInclusive:yyyy/MM/dd} → {range.ToInclusive:yyyy/MM/dd}";

        var charge = await _usageCharge.TryChargeReportExportAsync(companyNetworkId, user.Id, reportLabel, ct);
        if (!charge.Success)
        {
            return new BuildReportOutcome(null, charge.ErrorMessage ?? "تعذر تنفيذ العملية المالية.");
        }

        string[] headers;
        List<IReadOnlyList<string>> rows;
        switch (form.Kind)
        {
            case CompanyReportKind.Subscribers:
                (headers, rows) = await QuerySubscribersAsync(networkIds, range, ct);
                break;
            case CompanyReportKind.Sectors:
                (headers, rows) = await QuerySectorsAsync(networkIds, range, ct);
                break;
            case CompanyReportKind.Receivers:
                (headers, rows) = await QueryReceiversAsync(networkIds, range, ct);
                break;
            case CompanyReportKind.Servers:
                (headers, rows) = await QueryServersAsync(networkIds, range, ct);
                break;
            case CompanyReportKind.Subcontractors:
                (headers, rows) = await QuerySubcontractorsAsync(networkIds, range, ct);
                break;
            default:
                headers = [];
                rows = new List<IReadOnlyList<string>>();
                break;
        }

        var companyNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        var vars = ReportTemplateFormatter.BuildStandardPlaceholders(
            companyNetwork ?? selectedNetwork,
            selectedNetwork,
            form.Kind,
            range,
            rows.Count,
            user,
            DateTime.Now);

        var templateRow = await _db.NetworkReportTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == form.Kind, ct);

        var useCustom = !string.IsNullOrWhiteSpace(templateRow?.BodyContent?.Trim());
        string? before = null;
        string? after = null;
        if (useCustom)
        {
            var merged = ReportTemplateFormatter.ReplacePlaceholders(templateRow!.BodyContent, vars);
            (before, after) = ReportTemplateFormatter.SplitAtDataTable(merged);
        }

        var vm = new CompanyReportsResultViewModel
        {
            Title = ReportTemplateFormatter.GetReportTitleDisplay(form.Kind),
            NetworkName = selectedNetwork.Name ?? "",
            Range = range,
            Headers = headers,
            Rows = rows,
            ChargedAmountSyp = charge.ChargedAmountSyp,
            UseCustomTemplate = useCustom,
            CustomHtmlBeforeTable = before,
            CustomHtmlAfterTable = after
        };

        return new BuildReportOutcome(vm, null);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QuerySubscribersAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        CancellationToken ct)
    {
        var list = await _db.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId.HasValue && networkIds.Contains(c.NetworkId.Value))
            .Where(c => c.CreatedDate >= range.FromInclusive && c.CreatedDate <= range.ToInclusive)
            .Include(c => c.Profile)
            .Include(c => c.Receiver)
            .Include(c => c.MikroTikServer)
            .Include(c => c.Network)
            .OrderBy(c => c.Id)
            .ToListAsync(ct);

        string[] headers =
        [
            "المعرف", "الاسم", "الرقم الوطني", "اسم المستخدم", "الهاتف", "البروفايل", "المستقبل", "خادم MikroTik",
            "الشبكة", "تاريخ الإنشاء", "نشط", "العنوان", "انتهاء الصلاحية", "الرصيد"
        ];

        var rows = new List<IReadOnlyList<string>>(list.Count);
        foreach (var c in list)
        {
            rows.Add(new[]
            {
                c.Id.ToString(),
                c.Name ?? "",
                c.SID ?? "",
                c.UserName ?? "",
                c.PhoneNumber ?? "",
                c.Profile?.Name ?? c.ProfileName ?? "",
                c.Receiver?.Name ?? "",
                c.MikroTikServer?.Host ?? "",
                c.Network?.Name ?? "",
                c.CreatedDate.ToString("yyyy/MM/dd HH:mm"),
                c.IsActive ? "نعم" : "لا",
                c.ResidenceAddress ?? "",
                c.AccountExpirationDate?.ToString("yyyy/MM/dd") ?? "",
                c.Balance.ToString("N2")
            });
        }

        return (headers, rows);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QuerySectorsAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        CancellationToken ct)
    {
        var list = await _db.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Where(s => s.CreatedDate >= range.FromInclusive && s.CreatedDate <= range.ToInclusive)
            .Include(s => s.MikroTikServer)
            .Include(s => s.Network)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        string[] headers =
        [
            "المعرف", "الاسم", "IP", "خط العرض", "خط الطول", "الارتفاع (م)", "الاتجاه", "زاوية الانتشار", "مدى (كم)",
            "خادم MikroTik", "الشبكة", "تاريخ الإنشاء", "نشط"
        ];

        var rows = new List<IReadOnlyList<string>>(list.Count);
        foreach (var s in list)
        {
            rows.Add(new[]
            {
                s.Id.ToString(),
                s.Name ?? "",
                s.IPAddress ?? "",
                s.Latitude.ToString("F6"),
                s.Longitude.ToString("F6"),
                s.ElevationMeters?.ToString("F2") ?? "",
                s.Direction.ToString("F1"),
                s.CoverageAngle.ToString("F1"),
                s.CoverageRange.ToString("F2"),
                s.MikroTikServer?.Host ?? "",
                s.Network?.Name ?? "",
                s.CreatedDate.ToString("yyyy/MM/dd HH:mm"),
                s.IsActive ? "نعم" : "لا"
            });
        }

        return (headers, rows);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QueryReceiversAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        CancellationToken ct)
    {
        var list = await _db.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId.HasValue && networkIds.Contains(r.NetworkId.Value))
            .Where(r => r.CreatedDate >= range.FromInclusive && r.CreatedDate <= range.ToInclusive)
            .Include(r => r.Sector)
            .Include(r => r.Network)
            .OrderBy(r => r.Id)
            .ToListAsync(ct);

        string[] headers =
        [
            "المعرف", "الاسم", "IP", "قناع الشبكة", "القطاع", "الشبكة", "خط العرض", "خط الطول",
            "عدد المشتركين", "تاريخ الإنشاء", "نشط"
        ];

        var rows = new List<IReadOnlyList<string>>(list.Count);
        foreach (var r in list)
        {
            rows.Add(new[]
            {
                r.Id.ToString(),
                r.Name ?? "",
                r.IPAddress ?? "",
                r.NetworkMask ?? "",
                r.Sector?.Name ?? "",
                r.Network?.Name ?? "",
                r.Latitude.ToString("F6"),
                r.Longitude.ToString("F6"),
                r.UserCount.ToString(),
                r.CreatedDate.ToString("yyyy/MM/dd HH:mm"),
                r.IsActive ? "نعم" : "لا"
            });
        }

        return (headers, rows);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QueryServersAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        CancellationToken ct)
    {
        var list = await _db.MikroTikServers
            .AsNoTracking()
            .Where(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Where(s => s.CreatedAt >= range.FromInclusive && s.CreatedAt <= range.ToInclusive)
            .Include(s => s.Network)
            .OrderBy(s => s.Id)
            .ToListAsync(ct);

        string[] headers =
        [
            "المعرف", "الاسم", "المضيف", "المنفذ", "المستخدم", "ملاحظات", "الشبكة", "تاريخ الإنشاء", "نشط"
        ];

        var rows = new List<IReadOnlyList<string>>(list.Count);
        foreach (var s in list)
        {
            rows.Add(new[]
            {
                s.Id.ToString(),
                s.Name,
                s.Host,
                s.Port.ToString(),
                s.User,
                s.Notes ?? "",
                s.Network?.Name ?? "",
                s.CreatedAt.ToString("yyyy/MM/dd HH:mm"),
                s.IsActive ? "نعم" : "لا"
            });
        }

        return (headers, rows);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QuerySubcontractorsAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        CancellationToken ct)
    {
        var list = await _db.CollectionPointAccounts
            .AsNoTracking()
            .Where(a => a.NetworkId.HasValue && networkIds.Contains(a.NetworkId.Value))
            .Where(a => a.CreatedAt >= range.FromInclusive && a.CreatedAt <= range.ToInclusive)
            .Include(a => a.User)
            .Include(a => a.Network)
            .OrderBy(a => a.Id)
            .ToListAsync(ct);

        string[] headers =
        [
            "معرف الحساب", "اسم المستخدم", "الاسم الكامل", "البريد", "الهاتف", "الشبكة", "الرصيد", "تاريخ الإنشاء"
        ];

        var rows = new List<IReadOnlyList<string>>(list.Count);
        foreach (var a in list)
        {
            rows.Add(new[]
            {
                a.Id.ToString(),
                a.User?.UserName ?? "",
                a.User?.FullName ?? "",
                a.User?.Email ?? "",
                a.User?.PhoneNumber ?? "",
                a.Network?.Name ?? "",
                a.Balance.ToString("N2"),
                a.CreatedAt.ToString("yyyy/MM/dd HH:mm")
            });
        }

        return (headers, rows);
    }

    private static byte[] BuildExcelBytes(CompanyReportsResultViewModel vm)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("تقرير");
        ws.RightToLeft = true;

        var row = 1;
        if (vm.UseCustomTemplate)
        {
            row = AppendExcelPlainLines(ws, row, ReportTemplateFormatter.StripHtmlForPlainText(vm.CustomHtmlBeforeTable));
            if (row > 1)
            {
                row++;
            }
        }
        else
        {
            ws.Cell(row++, 1).Value = vm.Title;
            ws.Cell(row++, 1).Value = vm.NetworkName;
            ws.Cell(row++, 1).Value =
                $"الفترة: {vm.Range.FromInclusive:yyyy/MM/dd HH:mm} — {vm.Range.ToInclusive:yyyy/MM/dd HH:mm}";
            row++;
        }

        var col = 1;
        foreach (var h in vm.Headers)
        {
            ws.Cell(row, col).Value = h;
            col++;
        }

        row++;
        foreach (var r in vm.Rows)
        {
            col = 1;
            foreach (var cell in r)
            {
                ws.Cell(row, col).Value = cell;
                col++;
            }

            row++;
        }

        if (vm.UseCustomTemplate && !string.IsNullOrWhiteSpace(vm.CustomHtmlAfterTable))
        {
            row++;
            AppendExcelPlainLines(ws, row, ReportTemplateFormatter.StripHtmlForPlainText(vm.CustomHtmlAfterTable));
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static int AppendExcelPlainLines(IXLWorksheet ws, int startRow, string plain)
    {
        if (string.IsNullOrWhiteSpace(plain))
        {
            return startRow;
        }

        var r = startRow;
        foreach (var line in plain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ws.Cell(r, 1).Value = line;
            r++;
        }

        return r;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return sb.Length == 0 ? "report.xlsx" : sb.ToString();
    }
}
