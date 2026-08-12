using ClosedXML.Excel;

using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using RadaTik.Areas.CompanyAdmin.ViewModels;

using global::RadaTik.Constants;

using global::RadaTik.Data;

using global::RadaTik.Helpers;

using global::RadaTik.Models;

using global::RadaTik.Models.Business;

using global::RadaTik.Security;



namespace RadaTik.Areas.CompanyAdmin.Controllers;



[Area("CompanyAdmin")]

[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]

[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.MoneyDiary)]

public class MoneyDiaryController : Controller

{

    private readonly ApplicationDbContext _context;

    private readonly UserManager<ApplicationUser> _userManager;



    public MoneyDiaryController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)

    {

        _context = context;

        _userManager = userManager;

    }



    [HttpGet]

    public async Task<IActionResult> Index(

      int? year,

      int? month,

      decimal? prefillAmount,

      int? prefillEntryType,

      string? prefillCategoryKey,

      string? prefillDescription)

    {

        ViewData["Title"] = "دفتر الإيراد والمصروف";

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(

          HttpContext, _context, _userManager, User);

        if (scope == null)

        {

            TempData["Error"] = AppMessages.SelectNetworkFirst;

            return RedirectToAction("Index", "Network");

        }



        (int y, int m) = NormalizeYearMonth(year, month);

        MoneyDiaryIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);



        ViewBag.CompanyName = scope.CompanyNetworkName;

        ViewBag.BusinessModuleTitle = "دفتر الإيراد والمصروف";

        ViewBag.BusinessModuleHint =

          "تقرير شهري منفصل عن محفظة RadaTik. القيود اليدوية يمكن ربطها بالصندوق اختيارياً. فواتير «من المحفظة» لا تُسجَّل هنا.";



        if (prefillAmount.HasValue && prefillAmount.Value > 0m)

        {

            ViewBag.PrefillEntryType = prefillEntryType ?? (int)MoneyDiaryEntryType.Expense;

            ViewBag.PrefillCategoryKey = string.IsNullOrWhiteSpace(prefillCategoryKey) ? "expense_salary" : prefillCategoryKey;

            ViewBag.PrefillAmount = prefillAmount.Value;

            ViewBag.PrefillDescription = prefillDescription;

            ViewBag.ShowPayrollDiaryHint = prefillEntryType == (int)MoneyDiaryEntryType.Expense

              && string.Equals(prefillCategoryKey, "expense_salary", StringComparison.Ordinal);

            ViewBag.ShowMaintenanceDiaryHint = string.Equals(prefillCategoryKey, "income_maintenance", StringComparison.Ordinal)

              || string.Equals(prefillCategoryKey, "expense_maintenance", StringComparison.Ordinal);

        }



        return View(vm);

    }



    [HttpGet]

    public async Task<IActionResult> Print(int? year, int? month)

    {

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(

          HttpContext, _context, _userManager, User);

        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        (int y, int m) = NormalizeYearMonth(year, month);

        MoneyDiaryIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);

        ViewBag.CompanyName = scope.CompanyNetworkName;

        ViewBag.PrintTitle = "دفتر الإيراد والمصروف";

        return View("Print", vm);

    }



    [HttpGet]

    public async Task<IActionResult> ExportExcel(int? year, int? month)

    {

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(

          HttpContext, _context, _userManager, User);

        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        (int y, int m) = NormalizeYearMonth(year, month);

        MoneyDiaryIndexViewModel vm = await LoadMonthViewModelAsync(scope.CompanyNetworkId, y, m);

        string fileName = CompanyBusinessExcelHelper.SanitizeFileName($"دفتر_{scope.CompanyNetworkName}_{y}_{m:D2}.xlsx");



        byte[] bytes = CompanyBusinessExcelHelper.BuildWorkbook(ws =>

        {

            ws.Cell(1, 1).Value = $"دفتر الإيراد والمصروف — {scope.CompanyNetworkName} — {y}/{m}";

            ws.Cell(2, 1).Value =

          $"ل.س.ج: إيراد {vm.TotalIncomeSyp:N2} | مصروف {vm.TotalExpenseSyp:N2} | صافي {vm.NetSyp:N2}";

            ws.Cell(3, 1).Value =

          $"$: إيراد {vm.TotalIncomeUsd:N2} | مصروف {vm.TotalExpenseUsd:N2} | صافي {vm.NetUsd:N2}";

            int row = 5;

            ws.Cell(row, 1).Value = "التاريخ";

            ws.Cell(row, 2).Value = "النوع";

            ws.Cell(row, 3).Value = "التصنيف";

            ws.Cell(row, 4).Value = "العملة";

            ws.Cell(row, 5).Value = "المبلغ";

            ws.Cell(row, 6).Value = "الوصف";

            ws.Row(row).Style.Font.Bold = true;

            row++;

            foreach (MoneyDiaryEntry e in vm.Entries.OrderBy(x => x.EntryDate))

            {

                ws.Cell(row, 1).Value = e.EntryDate.ToString("yyyy-MM-dd");

                ws.Cell(row, 2).Value = e.EntryType == MoneyDiaryEntryType.Income ? "إيراد" : "مصروف";

                ws.Cell(row, 3).Value = MoneyDiaryCategories.GetLabel(e.CategoryKey);

                ws.Cell(row, 4).Value = CurrencyHelper.GetSymbol(e.Currency);

                ws.Cell(row, 5).Value = e.Amount;

                ws.Cell(row, 6).Value = e.Description ?? "";

                row++;

            }

        });



        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);

    }



    private static (int Year, int Month) NormalizeYearMonth(int? year, int? month)

    {

        int y = year ?? DateTime.Today.Year;

        int m = month ?? DateTime.Today.Month;

        if (m < 1)
        {
            m = 1;
        }

        if (m > 12)
        {
            m = 12;
        }

        return (y, m);

    }



    private static MoneyDiaryIndexViewModel LoadTotals(int year, int month, List<MoneyDiaryEntry> entries) =>

      new()

      {

          Year = year,

          Month = month,

          TotalIncomeSyp = SumByCurrency(entries, MoneyDiaryEntryType.Income, PricingCurrency.SYP_New),

          TotalExpenseSyp = SumByCurrency(entries, MoneyDiaryEntryType.Expense, PricingCurrency.SYP_New),

          TotalIncomeUsd = SumByCurrency(entries, MoneyDiaryEntryType.Income, PricingCurrency.USD),

          TotalExpenseUsd = SumByCurrency(entries, MoneyDiaryEntryType.Expense, PricingCurrency.USD),

          Entries = entries

      };



    private static decimal SumByCurrency(

      IEnumerable<MoneyDiaryEntry> entries,

      MoneyDiaryEntryType type,

      PricingCurrency currency) =>

      entries.Where(e => e.EntryType == type && e.Currency == currency).Sum(e => e.Amount);



    private async Task<MoneyDiaryIndexViewModel> LoadMonthViewModelAsync(

      int companyNetworkId,

      int year,

      int month)

    {

        DateTime from = new DateTime(year, month, 1);

        DateTime to = from.AddMonths(1);

        List<MoneyDiaryEntry> entries = await _context.MoneyDiaryEntries

          .AsNoTracking()

          .Include(e => e.CreatedByUser)

          .Where(e => e.CompanyNetworkId == companyNetworkId && e.EntryDate >= from && e.EntryDate < to)

          .OrderByDescending(e => e.EntryDate)

          .ThenByDescending(e => e.Id)

          .ToListAsync();



        return LoadTotals(year, month, entries);

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> AddEntry(

      MoneyDiaryEntryType entryType,

      string categoryKey,

      decimal amount,

      PricingCurrency currency,

      DateTime? entryDate,

      string? description,

      bool syncWithCashBox = false)

    {

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(

          HttpContext, _context, _userManager, User);

        if (scope == null)

        {

            TempData["Error"] = AppMessages.SelectNetworkFirst;

            return RedirectToAction(nameof(Index));

        }



        if (amount <= 0m)

        {

            TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";

            return RedirectToAction(nameof(Index));

        }



        categoryKey = (categoryKey ?? "").Trim();

        if (!MoneyDiaryCategories.All.Any(c => c.Key == categoryKey && c.EntryType == entryType))

        {

            TempData["Error"] = "اختر تصنيفاً صالحاً.";

            return RedirectToAction(nameof(Index));

        }



        PricingCurrency opCurrency = CashBoxHelper.NormalizeOperatingCurrency(currency);

        ApplicationUser? user = await _userManager.GetUserAsync(User);

        DateTime date = entryDate?.Date ?? DateTime.Today;



        MoneyDiaryEntry entry = new()

        {

            CompanyNetworkId = scope.CompanyNetworkId,

            EntryType = entryType,

            CategoryKey = categoryKey,

            Amount = amount,

            Currency = opCurrency,

            EntryDate = date,

            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),

            CreatedByUserId = user?.Id

        };

        _context.MoneyDiaryEntries.Add(entry);

        await _context.SaveChangesAsync();



        if (syncWithCashBox)

        {

            string? cashError = await TrySyncEntryWithCashBoxAsync(

              scope.CompanyNetworkId,

              entry,

              user?.Id ?? string.Empty);

            if (cashError != null)

            {

                TempData["Success"] = "تم تسجيل القيد في الدفتر.";

                TempData["Error"] = cashError;

                return RedirectToAction(nameof(Index), new { year = date.Year, month = date.Month });

            }



            TempData["Success"] = "تم تسجيل القيد وتحديث الصندوق النقدي.";

            return RedirectToAction(nameof(Index), new { year = date.Year, month = date.Month });

        }



        TempData["Success"] = "تم تسجيل القيد (الدفتر فقط — دون المحفظة).";

        return RedirectToAction(nameof(Index), new { year = date.Year, month = date.Month });

    }



    private async Task<string?> TrySyncEntryWithCashBoxAsync(

      int companyNetworkId,

      MoneyDiaryEntry entry,

      string userId)

    {

        if (string.IsNullOrWhiteSpace(userId))

        {

            return "تعذر ربط الصندوق: المستخدم غير معروف.";

        }



        CashBox? box = await CashBoxHelper.GetOrCreateCashBoxAsync(

          _context, CashBoxOwnerType.Network, companyNetworkId);

        if (box == null)

        {

            return "تعذر الوصول للصندوق النقدي.";

        }



        string diaryRef = $"دفتر #{entry.Id}";

        PricingCurrency currency = entry.Currency;



        if (entry.EntryType == MoneyDiaryEntryType.Income)

        {

            decimal before = CashBoxHelper.GetBalance(box, currency);

            CashBoxHelper.ApplyDelta(box, currency, entry.Amount);

            _context.CashBoxDeposits.Add(new CashBoxDeposit

            {

                CashBoxId = box.Id,

                Amount = entry.Amount,

                Currency = currency,

                DepositedAt = entry.EntryDate,

                DepositedByUserId = userId,

                Notes = $"{diaryRef} — {MoneyDiaryCategories.GetLabel(entry.CategoryKey)}",

                BalanceBefore = before,

                BalanceAfter = CashBoxHelper.GetBalance(box, currency)

            });

            await _context.SaveChangesAsync();

            return null;

        }



        if (!CashBoxHelper.HasSufficientBalance(box, currency, entry.Amount))

        {

            return CashBoxHelper.FormatInsufficientBalanceMessage(box, currency, entry.Amount) +

                   " (القيد مسجّل في الدفتر دون تحريك الصندوق).";

        }



        decimal balanceBefore = CashBoxHelper.GetBalance(box, currency);

        CashBoxHelper.ApplyDelta(box, currency, -entry.Amount);

        _context.CashBoxWithdrawals.Add(new CashBoxWithdrawal

        {

            CashBoxId = box.Id,

            Amount = entry.Amount,

            Currency = currency,

            WithdrawnAt = entry.EntryDate,

            WithdrawnByUserId = userId,

            Notes = $"{diaryRef} — {MoneyDiaryCategories.GetLabel(entry.CategoryKey)}",

            BalanceBefore = balanceBefore,

            BalanceAfter = CashBoxHelper.GetBalance(box, currency)

        });

        await _context.SaveChangesAsync();

        return null;

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> DeleteEntry(int id, int year, int month)

    {

        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(

          HttpContext, _context, _userManager, User);

        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        MoneyDiaryEntry? entry = await _context.MoneyDiaryEntries

          .FirstOrDefaultAsync(e => e.Id == id && e.CompanyNetworkId == scope.CompanyNetworkId);

        if (entry == null)

        {

            TempData["Error"] = "القيد غير موجود.";

            return RedirectToAction(nameof(Index), new { year, month });

        }



        if (entry.MaterialPurchaseInvoiceId.HasValue || entry.MaterialSalesInvoiceId.HasValue)

        {

            TempData["Error"] = "لا يمكن حذف قيد مرتبط بفاتورة مواد من هنا — عدّل الفاتورة.";

            return RedirectToAction(nameof(Index), new { year, month });

        }



        _context.MoneyDiaryEntries.Remove(entry);

        await _context.SaveChangesAsync();

        TempData["Success"] = "تم حذف القيد.";

        return RedirectToAction(nameof(Index), new { year, month });

    }

}


