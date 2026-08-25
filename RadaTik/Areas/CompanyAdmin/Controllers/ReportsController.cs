using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Services;
using global::RadaTik.Services.Documents;
using global::RadaTik.Services.Reports;
using global::RadaTik.ViewModels.CompanyAdmin;
using System.Globalization;
using System.Text;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Reports)]
public sealed partial class ReportsController : Controller
{
    /// <summary>يُرجَع مع ملف Excel بعد التصدير لتحديث رصيد الهيدر دون إعادة تحميل الصفحة.</summary>
    private const string CompanyWalletBalanceHeaderName = "X-Company-Wallet-Balance";

    private const string DefaultTemplateSample = """
<div style="text-align:center;direction:rtl">
  <h2>{{ReportTitle}}</h2>
  <p>الشركة: <strong>{{CompanyName}}</strong> — الشبكة المحددة: <strong>{{NetworkName}}</strong></p>
  <p>الفترة: من {{PeriodFrom}} إلى {{PeriodTo}} — عدد السجلات: {{RowCount}}</p>
</div>
{{DATA_TABLE}}
<p style="font-size:12px;color:#666;text-align:center">أُعدّ في {{GeneratedAt}}</p>
""";

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUsageBasedSubscriptionChargeService _usageCharge;
    private readonly ICompanyDocumentAppearanceService _documentAppearance;

    public ReportsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IUsageBasedSubscriptionChargeService usageCharge,
        ICompanyDocumentAppearanceService documentAppearance)
    {
        _db = db;
        _userManager = userManager;
        _usageCharge = usageCharge;
        _documentAppearance = documentAppearance;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "التقارير";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        int effectiveCompanyId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;

        ViewBag.ExportPriceSyp = await GetReportExportPriceHintSypAsync(HttpContext.RequestAborted);
        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_db, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;
        ViewBag.EffectiveCompanyNetworkId = effectiveCompanyId;

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Templates()
    {
        ViewData["Title"] = "قوالب التقارير";

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;

        Dictionary<CompanyReportKind, DateTime> existing = await _db.NetworkReportTemplates
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;

        NetworkReportTemplate? row = await _db.NetworkReportTemplates
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == kind);

        ViewBag.Kind = (int)kind;
        ViewBag.KindTitle = ReportTemplateFormatter.GetReportTitleDisplay(kind);
        ViewBag.BodyContent = row?.BodyContent ?? "";
        ViewBag.DefaultSample = DefaultTemplateSample;
        ViewBag.PrintColumns = ReportPrintColumns.Selectable(kind);
        ViewBag.SelectedPrintColumns = ReportPrintColumns.Deserialize(kind, row?.PrintedColumnKeys);
        ViewBag.Networks = await NetworkHelper.GetAvailableNetworksAsync(_db, user, _userManager);
        ViewBag.CurrentNetworkId = selectedNetworkId;

        return View();
    }

    public sealed class SaveTemplateForm
    {
        public CompanyReportKind Kind { get; set; }
        public string? BodyContent { get; set; }
        public List<string>? Columns { get; set; }
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        Network? selectedNetwork = await _db.Networks.FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return RedirectToAction(nameof(Templates));
        }

        int companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;

        string? body = string.IsNullOrWhiteSpace(form.BodyContent) ? null : form.BodyContent.Trim();
        string printedColumns = ReportPrintColumns.Serialize(form.Kind, form.Columns ?? []);
        NetworkReportTemplate? row = await _db.NetworkReportTemplates
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == form.Kind);

        DateTime now = DateTime.UtcNow;
        if (row == null)
        {
            _db.NetworkReportTemplates.Add(new NetworkReportTemplate
            {
                CompanyNetworkId = companyNetworkId,
                ReportKind = form.Kind,
                BodyContent = body,
                PrintedColumnKeys = printedColumns,
                UpdatedAt = now,
                UpdatedByUserId = user.Id
            });
        }
        else
        {
            row.BodyContent = body;
            row.PrintedColumnKeys = printedColumns;

            row.UpdatedAt = now;
            row.UpdatedByUserId = user.Id;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(EditTemplate), new { kind = form.Kind });
    }

    public sealed class ReportRunForm
    {
        public CompanyReportKind Kind { get; set; }
        public CompanyReportPeriodPreset Period { get; set; }
        public DateTime? CustomFrom { get; set; }
        public DateTime? CustomTo { get; set; }
        public List<string>? Columns { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Print(ReportRunForm form)
    {
        BuildReportOutcome result = await BuildReportAsync(form, applyExportCharge: false, chargeDescriptionSuffix: null, ct: HttpContext.RequestAborted);
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
        BuildReportOutcome result = await BuildReportAsync(
            form,
            applyExportCharge: true,
            chargeDescriptionSuffix: " — تصدير Excel",
            ct: HttpContext.RequestAborted);
        if (result.Error != null)
        {
            if (IsReportsAjaxRequest(Request))
            {
                return new JsonResult(new { ok = false, error = result.Error }) { StatusCode = 400 };
            }

            TempData["Error"] = result.Error;
            return RedirectToAction(nameof(Index));
        }

        CompanyReportsResultViewModel vm = result.ViewModel!;
        string safeName = SanitizeFileName($"{vm.Title}_{vm.Range.FromInclusive:yyyyMMdd}_{vm.Range.ToInclusive:yyyyMMdd}.xlsx");
        byte[] bytes = BuildExcelBytes(vm);

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user != null)
        {
            decimal? bal = await GetEffectiveCompanyWalletBalanceAsync(user, HttpContext.RequestAborted);
            if (bal.HasValue)
            {
                Response.Headers.Append(
                    CompanyWalletBalanceHeaderName,
                    bal.Value.ToString(CultureInfo.InvariantCulture));
            }
        }

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", safeName);
    }

    /// <summary>يُستدعى من صفحة نتيجة التقرير بعد تأكيد المستخدم؛ يخصم ثم يعيد JSON ليتابع العميل بـ window.print().</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChargeForPrint([FromForm] ReportRunForm form)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Json(new { ok = false, error = "يجب تسجيل الدخول.", chargedAmount = 0m });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            return Json(new { ok = false, error = "يرجى تحديد شبكة أولاً.", chargedAmount = 0m });
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value, HttpContext.RequestAborted);
        if (selectedNetwork == null)
        {
            return Json(new { ok = false, error = "تعذر العثور على الشبكة.", chargedAmount = 0m });
        }

        int companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        CompanyReportDateRange range = CompanyReportDateRange.Resolve(form.Period, form.CustomFrom, form.CustomTo);
        string reportLabel =
            $"{ReportTemplateFormatter.GetReportTitleDisplay(form.Kind)} — {range.FromInclusive:yyyy/MM/dd} → {range.ToInclusive:yyyy/MM/dd}";

        ReportExportChargeResult charge = await _usageCharge.TryChargeReportExportAsync(
            companyNetworkId,
            user.Id,
            reportLabel + " — طباعة",
            HttpContext.RequestAborted);

        if (!charge.Success)
        {
            return Json(new { ok = false, error = charge.ErrorMessage ?? "تعذر تنفيذ العملية المالية.", chargedAmount = 0m, newBalance = (decimal?)null });
        }

        decimal? newBalance = await GetEffectiveCompanyWalletBalanceAsync(user, HttpContext.RequestAborted);
        return Json(new
        {
            ok = true,
            error = (string?)null,
            chargedAmount = charge.ChargedAmountSyp,
            newBalance
        });
    }

    private sealed record BuildReportOutcome(CompanyReportsResultViewModel? ViewModel, string? Error);

    private static bool IsReportsAjaxRequest(HttpRequest request) =>
        string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

    /// <summary>نفس منطق عرض رصيد الهيدر لمدير الشركة (WalletBalanceViewComponent).</summary>
    private async Task<decimal?> GetEffectiveCompanyWalletBalanceAsync(ApplicationUser user, CancellationToken ct)
    {
        if (!User.IsInRole(RoleNames.NetworkAdministrator))
        {
            return null;
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            return null;
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value, ct);
        if (selectedNetwork == null)
        {
            return null;
        }

        int effectiveCompanyId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effective = effectiveCompanyId != selectedNetwork.Id
            ? await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveCompanyId, ct)
            : selectedNetwork;

        return effective?.Balance ?? 0m;
    }

    private async Task<decimal?> GetReportExportPriceHintSypAsync(CancellationToken ct)
    {
        decimal? raw = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p => p.IsActive && p.FeatureKey == FeatureKeys.ReportsExport)
            .OrderByDescending(p => p.Id)
            .Select(p => (decimal?)p.AmountSYP)
            .FirstOrDefaultAsync(ct);

        return raw.HasValue ? WalletMath.CeilSyp(raw.Value) : null;
    }

    private async Task<BuildReportOutcome> BuildReportAsync(
        ReportRunForm form,
        bool applyExportCharge,
        string? chargeDescriptionSuffix,
        CancellationToken ct)
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return new BuildReportOutcome(null, "يجب تسجيل الدخول.");
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _db, user);
        if (!selectedNetworkId.HasValue)
        {
            return new BuildReportOutcome(null, "يرجى تحديد شبكة أولاً.");
        }

        Network? selectedNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value, ct);
        if (selectedNetwork == null)
        {
            return new BuildReportOutcome(null, "تعذر العثور على الشبكة.");
        }

        int companyNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        List<int> networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);

        CompanyReportDateRange range = CompanyReportDateRange.Resolve(form.Period, form.CustomFrom, form.CustomTo);
        string reportLabel = $"{ReportTemplateFormatter.GetReportTitleDisplay(form.Kind)} — {range.FromInclusive:yyyy/MM/dd} → {range.ToInclusive:yyyy/MM/dd}";

        decimal? exportPriceHint = await GetReportExportPriceHintSypAsync(ct);
        ReportExportChargeResult? charge = null;
        if (applyExportCharge)
        {
            string chargeDescription = string.IsNullOrEmpty(chargeDescriptionSuffix)
                ? reportLabel
                : $"{reportLabel}{chargeDescriptionSuffix}";

            charge = await _usageCharge.TryChargeReportExportAsync(companyNetworkId, user.Id, chargeDescription, ct);
            if (!charge.Success)
            {
                return new BuildReportOutcome(null, charge.ErrorMessage ?? "تعذر تنفيذ العملية المالية.");
            }
        }

        IReadOnlyList<string> selectedColumns = await ResolveAndPersistColumnsAsync(
            companyNetworkId, user.Id, form.Kind, form.Columns, ct);
        string[] headers;
        List<IReadOnlyList<string>> rows;
        switch (form.Kind)
        {
            case CompanyReportKind.Subscribers:
                (headers, rows) = await QuerySubscribersAsync(networkIds, range, selectedColumns, ct);
                break;
            case CompanyReportKind.Sectors:
                (headers, rows) = await QuerySectorsAsync(networkIds, range, selectedColumns, ct);
                break;
            case CompanyReportKind.Receivers:
                (headers, rows) = await QueryReceiversAsync(networkIds, range, selectedColumns, ct);
                break;
            case CompanyReportKind.Servers:
                (headers, rows) = await QueryServersAsync(networkIds, range, selectedColumns, ct);
                break;
            case CompanyReportKind.Subcontractors:
                (headers, rows) = await QuerySubcontractorsAsync(networkIds, range, selectedColumns, ct);
                break;
            default:
                headers = [];
                rows = [];
                break;
        }

        Network? companyNetwork = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        IReadOnlyDictionary<string, string> varsBase = ReportTemplateFormatter.BuildStandardPlaceholders(
            companyNetwork ?? selectedNetwork,
            selectedNetwork,
            form.Kind,
            range,
            rows.Count,
            user,
            DateTime.Now);

        // لا يُعرض اسم المدير/بريده في مخرجات التقرير المطبوع أو المعاينة.
        Dictionary<string, string> vars = new Dictionary<string, string>(varsBase, StringComparer.OrdinalIgnoreCase)
        {
            ["ManagerName"] = "",
            ["ManagerEmail"] = "",
            ["ManagerUserName"] = ""
        };

        NetworkReportTemplate? templateRow = await _db.NetworkReportTemplates
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == form.Kind, ct);

        bool useCustom = !string.IsNullOrWhiteSpace(templateRow?.BodyContent?.Trim());
        string? sectorsTableClass = form.Kind == CompanyReportKind.Sectors ? "report-data-table-sectors" : null;
        string tableCaption = ReportTemplateFormatter.GetReportTitleDisplay(form.Kind);
        string dataTableHtml = ReportTemplateFormatter.BuildDataTableHtml(headers, rows, sectorsTableClass, tableCaption);
        string? integratedBodyHtml = null;
        string? before = null;
        string? after = null;
        if (useCustom && templateRow != null)
        {
            string merged = ReportTemplateFormatter.ReplacePlaceholders(templateRow.BodyContent, vars);
            if (merged.Contains(ReportTemplateFormatter.DataTableMarker, StringComparison.OrdinalIgnoreCase))
            {
                integratedBodyHtml = merged.Replace(
                    ReportTemplateFormatter.DataTableMarker,
                    dataTableHtml,
                    StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                (before, after) = ReportTemplateFormatter.SplitAtDataTable(merged);
            }
        }

        CompanyReportsResultViewModel vm = new CompanyReportsResultViewModel
        {
            Title = ReportTemplateFormatter.GetReportTitleDisplay(form.Kind),
            NetworkName = selectedNetwork.Name ?? "",
            Range = range,
            Headers = headers,
            Rows = rows,
            DataTableHtml = dataTableHtml,
            IntegratedBodyHtml = integratedBodyHtml,
            Kind = form.Kind,
            Period = form.Period,
            CustomFrom = form.CustomFrom,
            CustomTo = form.CustomTo,
            ExportPriceHintSyp = exportPriceHint,
            ChargedAmountSyp = charge?.Success == true ? charge.ChargedAmountSyp : null,
            UseCustomTemplate = useCustom,
            CustomHtmlBeforeTable = before,
            CustomHtmlAfterTable = after,
            SelectedColumnKeys = selectedColumns,
            Chrome = await _documentAppearance.GetChromeAsync(
                companyNetworkId,
                ReportTemplateFormatter.GetReportTitleDisplay(form.Kind),
                selectedNetwork.Name,
                DateTime.Now.ToString("yyyy/MM/dd HH:mm"),
                ct)
        };

        return new BuildReportOutcome(vm, null);
    }

    private async Task<IReadOnlyList<string>> ResolveAndPersistColumnsAsync(
        int companyNetworkId,
        string userId,
        CompanyReportKind kind,
        IEnumerable<string>? requested,
        CancellationToken ct)
    {
        NetworkReportTemplate? row = await _db.NetworkReportTemplates
            .FirstOrDefaultAsync(t => t.CompanyNetworkId == companyNetworkId && t.ReportKind == kind, ct);

        IReadOnlyList<string> selected = requested != null && requested.Any(k => !string.IsNullOrWhiteSpace(k))
            ? ReportPrintColumns.ResolveSelected(kind, requested)
            : ReportPrintColumns.Deserialize(kind, row?.PrintedColumnKeys);

        string serialized = ReportPrintColumns.Serialize(kind, selected);
        if (row == null)
        {
            _db.NetworkReportTemplates.Add(new NetworkReportTemplate
            {
                CompanyNetworkId = companyNetworkId,
                ReportKind = kind,
                PrintedColumnKeys = serialized,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = userId
            });
            await _db.SaveChangesAsync(ct);
        }
        else if (!string.Equals(row.PrintedColumnKeys, serialized, StringComparison.Ordinal))
        {
            row.PrintedColumnKeys = serialized;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedByUserId = userId;
            await _db.SaveChangesAsync(ct);
        }

        return selected;
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QuerySubscribersAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        IReadOnlyList<string> selectedColumns,
        CancellationToken ct)
    {
        List<Client> list = await _db.Clients
            .AsNoTracking()
            .Where(c => c.NetworkId.HasValue && networkIds.Contains(c.NetworkId.Value))
            .Where(c => c.CreatedDate >= range.FromInclusive && c.CreatedDate <= range.ToInclusive)
            .Include(c => c.Profile)
            .Include(c => c.Receiver)
            .Include(c => c.Network)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Id)
            .ToListAsync(ct);

        return ReportPrintColumns.BuildSubscribers(list, selectedColumns);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QuerySectorsAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        IReadOnlyList<string> selectedColumns,
        CancellationToken ct)
    {
        List<Sector> list = await _db.Sectors
            .AsNoTracking()
            .Where(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Where(s => s.CreatedDate >= range.FromInclusive && s.CreatedDate <= range.ToInclusive)
            .Include(s => s.Network)
            .Include(s => s.MikroTikServer)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        return ReportPrintColumns.BuildSectors(list, selectedColumns);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QueryReceiversAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        IReadOnlyList<string> selectedColumns,
        CancellationToken ct)
    {
        List<Receiver> list = await _db.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId.HasValue && networkIds.Contains(r.NetworkId.Value))
            .Where(r => r.CreatedDate >= range.FromInclusive && r.CreatedDate <= range.ToInclusive)
            .Include(r => r.Sector)
            .Include(r => r.Network)
            .Include(r => r.Clients)
            .OrderBy(r => r.Name)
            .ThenBy(r => r.Id)
            .ToListAsync(ct);

        return ReportPrintColumns.BuildReceivers(list, selectedColumns);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QueryServersAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        IReadOnlyList<string> selectedColumns,
        CancellationToken ct)
    {
        List<MikroTikServer> list = await _db.MikroTikServers
            .AsNoTracking()
            .Where(s => s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
            .Where(s => s.CreatedAt >= range.FromInclusive && s.CreatedAt <= range.ToInclusive)
            .Include(s => s.Network)
            .OrderBy(s => s.Name)
            .ThenBy(s => s.Id)
            .ToListAsync(ct);

        return ReportPrintColumns.BuildServers(list, selectedColumns);
    }

    private async Task<(string[] Headers, List<IReadOnlyList<string>> Rows)> QuerySubcontractorsAsync(
        List<int> networkIds,
        CompanyReportDateRange range,
        IReadOnlyList<string> selectedColumns,
        CancellationToken ct)
    {
        List<CollectionPointAccount> list = await _db.CollectionPointAccounts
            .AsNoTracking()
            .Where(a => a.NetworkId.HasValue && networkIds.Contains(a.NetworkId.Value))
            .Where(a => a.CreatedAt >= range.FromInclusive && a.CreatedAt <= range.ToInclusive)
            .Include(a => a.User)
            .Include(a => a.Network)
            .OrderBy(a => a.User != null ? a.User.UserName : "")
            .ThenBy(a => a.Id)
            .ToListAsync(ct);

        return ReportPrintColumns.BuildSubcontractors(list, selectedColumns);
    }

    private static byte[] BuildExcelBytes(CompanyReportsResultViewModel vm)
    {
        using XLWorkbook wb = new XLWorkbook();
        IXLWorksheet ws = wb.Worksheets.Add("تقرير");
        ws.RightToLeft = true;

        int row = 1;
        if (!string.IsNullOrEmpty(vm.IntegratedBodyHtml))
        {
            ws.Cell(row++, 1).Value = vm.Title;
            ws.Cell(row++, 1).Value = vm.NetworkName;
            ws.Cell(row++, 1).Value =
                $"الفترة: {vm.Range.FromInclusive:yyyy/MM/dd HH:mm} — {vm.Range.ToInclusive:yyyy/MM/dd HH:mm}";
            row++;
        }
        else if (vm.UseCustomTemplate)
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

        int col = 1;
        foreach (string h in vm.Headers)
        {
            ws.Cell(row, col).Value = h;
            col++;
        }

        row++;
        foreach (IReadOnlyList<string> r in vm.Rows)
        {
            col = 1;
            foreach (string cell in r)
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

        using MemoryStream ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static int AppendExcelPlainLines(IXLWorksheet ws, int startRow, string plain)
    {
        if (string.IsNullOrWhiteSpace(plain))
        {
            return startRow;
        }

        int r = startRow;
        foreach (string line in plain.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            ws.Cell(r, 1).Value = line;
            r++;
        }

        return r;
    }

    private static string SanitizeFileName(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        StringBuilder sb = new StringBuilder(name.Length);
        foreach (char ch in name)
        {
            sb.Append(invalid.Contains(ch) ? '_' : ch);
        }

        return sb.Length == 0 ? "report.xlsx" : sb.ToString();
    }
}
