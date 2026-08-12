using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class CashBoxController : Controller
{
    private const int SystemAdminOwnerId = 0;

    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CashBoxController> _logger;

    public CashBoxController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CashBoxController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "الصندوق النقدي";
        var cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.SystemAdmin, SystemAdminOwnerId);
        if (cashBox == null)
        {
            TempData["Error"] = "تعذر تهيئة الصندوق النقدي.";
            return RedirectToAction("Index", "SystemAdmin", new { area = "SystemAdmin" });
        }

        await _context.Entry(cashBox)
            .Collection(c => c.Withdrawals!)
            .Query()
            .Include(w => w.WithdrawnByUser)
            .OrderByDescending(w => w.WithdrawnAt)
            .LoadAsync();
        await _context.Entry(cashBox)
            .Collection(c => c.Deposits!)
            .Query()
            .Include(d => d.DepositedByUser)
            .OrderByDescending(d => d.DepositedAt)
            .LoadAsync();

        ViewBag.CashBox = cashBox;
        ViewBag.OwnerName = "مدير النظام";
        ViewBag.Withdrawals = cashBox.Withdrawals?.ToList() ?? new List<CashBoxWithdrawal>();
        ViewBag.Deposits = cashBox.Deposits?.ToList() ?? new List<CashBoxDeposit>();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(decimal amount, string? notes)
    {
        var cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.SystemAdmin, SystemAdminOwnerId);
        if (cashBox == null)
        {
            TempData["Error"] = "تعذر تهيئة الصندوق النقدي.";
            return RedirectToAction(nameof(Index));
        }

        if (amount < 0.01m)
        {
            TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
            return RedirectToAction(nameof(Index));
        }

        if (cashBox.Balance < amount)
        {
            TempData["Error"] = $"رصيد الصندوق غير كافٍ. المتوفر: {cashBox.Balance:N2} ل.س";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يرجى تسجيل الدخول.";
            return RedirectToAction(nameof(Index));
        }

        var balanceBefore = cashBox.Balance;
        cashBox.Balance -= amount;
        cashBox.UpdatedAt = DateTime.Now;

        var withdrawal = new CashBoxWithdrawal
        {
            CashBoxId = cashBox.Id,
            Amount = amount,
            WithdrawnAt = DateTime.Now,
            WithdrawnByUserId = user.Id,
            Notes = notes?.Trim(),
            BalanceBefore = balanceBefore,
            BalanceAfter = cashBox.Balance
        };
        _context.CashBoxWithdrawals.Add(withdrawal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("سحب من صندوق مدير النظام #{CashBoxId} مبلغ {Amount} من قبل {UserId}", cashBox.Id, amount, user.Id);
        TempData["Success"] = $"تم سحب {amount:N2} ل.س من الصندوق. الرصيد الحالي: {cashBox.Balance:N2} ل.س";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(decimal amount, string? notes)
    {
        var cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.SystemAdmin, SystemAdminOwnerId);
        if (cashBox == null)
        {
            TempData["Error"] = "تعذر تهيئة الصندوق النقدي.";
            return RedirectToAction(nameof(Index));
        }

        if (amount < 0.01m)
        {
            TempData["Error"] = "المبلغ يجب أن يكون أكبر من صفر.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يرجى تسجيل الدخول.";
            return RedirectToAction(nameof(Index));
        }

        var balanceBefore = cashBox.Balance;
        cashBox.Balance += amount;
        cashBox.UpdatedAt = DateTime.Now;

        var deposit = new CashBoxDeposit
        {
            CashBoxId = cashBox.Id,
            Amount = amount,
            DepositedAt = DateTime.Now,
            DepositedByUserId = user.Id,
            Notes = notes?.Trim(),
            BalanceBefore = balanceBefore,
            BalanceAfter = cashBox.Balance
        };
        _context.CashBoxDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        _logger.LogInformation("إيداع في صندوق مدير النظام #{CashBoxId} مبلغ {Amount} من قبل {UserId}", cashBox.Id, amount, user.Id);
        TempData["Success"] = $"تم إيداع {amount:N2} ل.س في الصندوق. الرصيد الحالي: {cashBox.Balance:N2} ل.س";
        return RedirectToAction(nameof(Index));
    }
}
