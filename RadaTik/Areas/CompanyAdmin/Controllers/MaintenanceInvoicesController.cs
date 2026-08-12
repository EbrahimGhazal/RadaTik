using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

/// <summary>فواتير الصيانة — عرض وروابط اختيارية لدفتر الإيراد والمستودع (بدون ربط تلقائي).</summary>
[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Requests)]
public class MaintenanceInvoicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IFeatureAccessService _featureAccess;

    public MaintenanceInvoicesController(
      ApplicationDbContext context,
      UserManager<ApplicationUser> userManager,
      IFeatureAccessService featureAccess)
    {
        _context = context;
        _userManager = userManager;
        _featureAccess = featureAccess;
    }

    [HttpGet]
    public async Task<IActionResult> Index(MaintenanceInvoiceStatus? status)
    {
        ViewData["Title"] = "فواتير الصيانة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network");
        }

        List<int> networkScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, scope.CompanyNetworkId);
        IQueryable<MaintenanceInvoice> query = _context.MaintenanceInvoices
          .AsNoTracking()
          .Include(i => i.Client)
          .Include(i => i.MaintenanceRequest)
          .Where(i => networkScope.Contains(i.NetworkId));

        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        IQueryable<MaintenanceInvoice> scopeQuery = _context.MaintenanceInvoices
          .AsNoTracking()
          .Where(i => networkScope.Contains(i.NetworkId));

        ViewBag.PendingCount = await scopeQuery.CountAsync(i => i.Status == MaintenanceInvoiceStatus.Pending);
        ViewBag.PaidCount = await scopeQuery.CountAsync(i => i.Status == MaintenanceInvoiceStatus.Paid);

        List<MaintenanceInvoice> invoices = await query
          .OrderByDescending(i => i.CreatedAt)
          .Take(200)
          .ToListAsync();

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.CurrentStatus = status;
        ViewBag.CanMoneyDiary = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.MoneyDiary);
        ViewBag.CanWarehouse = await _featureAccess.HasFeatureAsync(User, HttpContext, FeatureKeys.Warehouse);
        ViewBag.BusinessModuleTitle = "فواتير الصيانة";
        ViewBag.BusinessModuleHint = "السداد يتم عبر محفظة العميل في RadaTik. التسجيل في الدفتر أو المستودع اختياري ولا يغيّر المحفظة.";

        return View(invoices);
    }

    [HttpGet]
    public async Task<IActionResult> ExportExcel(MaintenanceInvoiceStatus? status)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
          HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        List<int> networkScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, scope.CompanyNetworkId);
        IQueryable<MaintenanceInvoice> query = _context.MaintenanceInvoices
          .AsNoTracking()
          .Include(i => i.Client)
          .Where(i => networkScope.Contains(i.NetworkId));
        if (status.HasValue)
        {
            query = query.Where(i => i.Status == status.Value);
        }

        List<MaintenanceInvoice> invoices = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        string fileName = CompanyBusinessExcelHelper.SanitizeFileName($"فواتير_صيانة_{scope.CompanyNetworkName}.xlsx");

        byte[] bytes = CompanyBusinessExcelHelper.BuildWorkbook(ws =>
        {
            ws.Cell(1, 1).Value = $"فواتير الصيانة — {scope.CompanyNetworkName}";
            int row = 3;
            ws.Cell(row, 1).Value = "#";
            ws.Cell(row, 2).Value = "العميل";
            ws.Cell(row, 3).Value = "الإجمالي";
            ws.Cell(row, 4).Value = "صافي الشركة";
            ws.Cell(row, 5).Value = "الحالة";
            ws.Cell(row, 6).Value = "التاريخ";
            ws.Row(row).Style.Font.Bold = true;
            row++;
            foreach (MaintenanceInvoice i in invoices)
            {
                ws.Cell(row, 1).Value = i.Id;
                ws.Cell(row, 2).Value = i.Client?.Name ?? "";
                ws.Cell(row, 3).Value = i.GrossAmount;
                ws.Cell(row, 4).Value = i.NetAmountToCompany;
                ws.Cell(row, 5).Value = i.Status switch
                {
                    MaintenanceInvoiceStatus.Paid => "مدفوعة",
                    MaintenanceInvoiceStatus.Cancelled => "ملغاة",
                    _ => "معلقة"
                };
                ws.Cell(row, 6).Value = i.CreatedAt.ToString("yyyy-MM-dd");
                row++;
            }
        });

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
    }
}
