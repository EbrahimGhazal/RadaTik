using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Models.Business;
using global::RadaTik.Security;
using global::RadaTik.Services;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
[Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Erp)]
public class CompanyAccountingController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CompanyAccountingService _accountingService;

    public CompanyAccountingController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CompanyAccountingService accountingService)
    {
        _context = context;
        _userManager = userManager;
        _accountingService = accountingService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "المحاسبة";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        await _accountingService.EnsureDefaultChartOfAccountsAsync(scope.CompanyNetworkId);

        ViewBag.CompanyName = scope.CompanyNetworkName;
        ViewBag.Accounts = await _context.ChartOfAccounts.AsNoTracking()
            .Where(a => a.CompanyNetworkId == scope.CompanyNetworkId && a.IsActive)
            .OrderBy(a => a.Code)
            .ToListAsync();

        ViewBag.Entries = await _context.JournalEntries.AsNoTracking()
            .Where(e => e.CompanyNetworkId == scope.CompanyNetworkId)
            .OrderByDescending(e => e.EntryDate)
            .Take(50)
            .ToListAsync();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> CreateEntry()
    {
        ViewData["Title"] = "قيد محاسبي جديد";
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        await _accountingService.EnsureDefaultChartOfAccountsAsync(scope.CompanyNetworkId);
        await PopulateAccountsAsync(scope.CompanyNetworkId);

        return View(new JournalEntry
        {
            CompanyNetworkId = scope.CompanyNetworkId,
            EntryDate = DateTime.UtcNow.Date,
            Currency = PricingCurrency.SYP_New,
            Status = JournalEntryStatus.Draft,
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateEntry(
        JournalEntry model,
        int[] accountIds,
        decimal[] debits,
        decimal[] credits,
        string[]? lineDescriptions)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction("Index", "Network");
        }

        model.CompanyNetworkId = scope.CompanyNetworkId;
        model.Description = model.Description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(model.Description))
        {
            ModelState.AddModelError(nameof(model.Description), "وصف القيد مطلوب.");
        }

        List<JournalEntryLine> lines = BuildLines(accountIds, debits, credits, lineDescriptions);
        if (lines.Count < 2)
        {
            ModelState.AddModelError(string.Empty, "يجب إدخال سطرين على الأقل.");
        }

        decimal totalDebit = lines.Sum(l => l.Debit);
        decimal totalCredit = lines.Sum(l => l.Credit);
        if (lines.Count >= 2 && totalDebit != totalCredit)
        {
            ModelState.AddModelError(string.Empty, "مجموع المدين يجب أن يساوي مجموع الدائن.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateAccountsAsync(scope.CompanyNetworkId);
            return View(model);
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        model.CreatedByUserId = user?.Id;
        model.CreatedAt = DateTime.UtcNow;
        model.Status = JournalEntryStatus.Draft;
        model.Lines = lines;
        _context.JournalEntries.Add(model);
        await _context.SaveChangesAsync();
        TempData["Success"] = "تم حفظ القيد كمسودة.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostEntry(int id)
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await ResolveScopeAsync();
        if (scope == null)
        {
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        (bool success, string message) = await _accountingService.PostJournalEntryAsync(
            id, scope.CompanyNetworkId, user?.Id ?? string.Empty);

        TempData[success ? "Success" : "Error"] = message;
        return RedirectToAction(nameof(Index));
    }

    private static List<JournalEntryLine> BuildLines(
        int[] accountIds,
        decimal[] debits,
        decimal[] credits,
        string[]? lineDescriptions)
    {
        List<JournalEntryLine> lines = new List<JournalEntryLine>();
        int count = Math.Min(accountIds.Length, Math.Min(debits.Length, credits.Length));
        for (int i = 0; i < count; i++)
        {
            if (accountIds[i] <= 0 || (debits[i] == 0 && credits[i] == 0))
            {
                continue;
            }

            lines.Add(new JournalEntryLine
            {
                ChartOfAccountId = accountIds[i],
                Debit = debits[i],
                Credit = credits[i],
                LineDescription = lineDescriptions != null && i < lineDescriptions.Length
                    ? lineDescriptions[i]?.Trim()
                    : null,
            });
        }

        return lines;
    }

    private async Task<CompanyBusinessScopeHelper.CompanyScope?> ResolveScopeAsync()
    {
        CompanyBusinessScopeHelper.CompanyScope? scope = await CompanyBusinessScopeHelper.ResolveAsync(
            HttpContext, _context, _userManager, User);
        if (scope == null)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
        }

        return scope;
    }

    private async Task PopulateAccountsAsync(int companyNetworkId)
    {
        ViewBag.AccountsSelect = await _context.ChartOfAccounts.AsNoTracking()
            .Where(a => a.CompanyNetworkId == companyNetworkId && a.IsActive)
            .OrderBy(a => a.Code)
            .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = $"{a.Code} — {a.Name}" })
            .ToListAsync();
    }
}
