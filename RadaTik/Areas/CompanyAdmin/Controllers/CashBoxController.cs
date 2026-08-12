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

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class CashBoxController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly CashBoxExchangeService _exchangeService;
    private readonly ILogger<CashBoxController> _logger;

    public CashBoxController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        CashBoxExchangeService exchangeService,
        ILogger<CashBoxController> logger)
    {
        _context = context;
        _userManager = userManager;
        _exchangeService = exchangeService;
        _logger = logger;
    }

    private async Task<(int? effectiveNetworkId, string? effectiveNetworkName)?> GetEffectiveNetworkAsync()
    {
        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null || !user.NetworkId.HasValue)
        {
            return null;
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, user);
        if (!selectedNetworkId.HasValue)
        {
            return null;
        }

        Network? selectedNetwork = await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        if (selectedNetwork == null)
        {
            return null;
        }

        int effectiveNetworkId = selectedNetwork.ParentNetworkId ?? selectedNetwork.Id;
        Network? effectiveNetwork = selectedNetwork.ParentNetworkId.HasValue
            ? await _context.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == effectiveNetworkId)
            : selectedNetwork;
        return (effectiveNetworkId, effectiveNetwork?.Name ?? selectedNetwork.Name);
    }

    private static PricingCurrency ParseCurrency(int currencyValue) =>
        currencyValue == (int)PricingCurrency.USD ? PricingCurrency.USD : PricingCurrency.SYP_New;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        ViewData["Title"] = "الصندوق النقدي";
        (int? effectiveNetworkId, string? effectiveNetworkName)? networkInfo = await GetEffectiveNetworkAsync();
        if (!networkInfo.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction("Index", "Network", new { area = "CompanyAdmin" });
        }

        (int? effectiveNetworkId, string? effectiveNetworkName) = networkInfo.Value;
        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.Network, effectiveNetworkId!.Value);
        if (cashBox == null)
        {
            TempData["Error"] = "تعذر تهيئة الصندوق النقدي.";
            return RedirectToAction("Index", "Dashboard", new { area = "CompanyAdmin" });
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

        Network? company = await _context.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == effectiveNetworkId!.Value);

        List<CashBoxCurrencyExchange> exchanges = await _context.CashBoxCurrencyExchanges
            .AsNoTracking()
            .Where(e => e.CashBoxId == cashBox.Id)
            .Include(e => e.CreatedByUser)
            .OrderByDescending(e => e.CreatedAt)
            .Take(50)
            .ToListAsync();

        ViewBag.CashBox = cashBox;
        ViewBag.OwnerName = effectiveNetworkName ?? "الشبكة";
        ViewBag.Withdrawals = cashBox.Withdrawals?.ToList() ?? new List<CashBoxWithdrawal>();
        ViewBag.Deposits = cashBox.Deposits?.ToList() ?? new List<CashBoxDeposit>();
        ViewBag.Exchanges = exchanges;
        ViewBag.WalletBalanceSyp = company?.Balance ?? 0m;
        ViewBag.WalletBalanceUsd = company?.BalanceUsd ?? 0m;
        ViewBag.DefaultExchangeRate = company?.DefaultUsdToSypExchangeRate;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Exchange(
        int fromCurrency,
        decimal sourceAmount,
        decimal exchangeRate,
        string? notes)
    {
        (int? effectiveNetworkId, string? effectiveNetworkName)? networkInfo = await GetEffectiveNetworkAsync();
        if (!networkInfo.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يرجى تسجيل الدخول.";
            return RedirectToAction(nameof(Index));
        }

        PricingCurrency from = ParseCurrency(fromCurrency);
        CashBoxExchangeResult result = await _exchangeService.ExecuteExchangeAsync(
            networkInfo.Value.effectiveNetworkId!.Value,
            user.Id,
            from,
            sourceAmount,
            exchangeRate,
            notes,
            HttpContext.RequestAborted);

        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        PricingCurrency to = CashBoxHelper.GetOppositeOperatingCurrency(from);
        decimal target;
        try
        {
            target = CashBoxHelper.ComputeExchangeTargetAmount(sourceAmount, from, exchangeRate);
        }
        catch
        {
            target = 0m;
        }

        _logger.LogInformation(
            "تحويل صندوق #{ExchangeId}: {Source} {From} → {Target} {To} بسعر {Rate}",
            result.ExchangeId, sourceAmount, from, target, to, exchangeRate);

        TempData["Success"] =
            $"تم التحويل: {CurrencyHelper.FormatAmount(sourceAmount, from)} ← {CurrencyHelper.FormatAmount(target, to)} (1 $ = {exchangeRate:N2} ل.س.ج)";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Withdraw(decimal amount, int currency, string? notes)
    {
        (int? effectiveNetworkId, string? effectiveNetworkName)? networkInfo = await GetEffectiveNetworkAsync();
        if (!networkInfo.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        PricingCurrency opCurrency = ParseCurrency(currency);
        (int? effectiveNetworkId, string? _) = networkInfo.Value;
        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.Network, effectiveNetworkId!.Value);
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

        if (!CashBoxHelper.HasSufficientBalance(cashBox, opCurrency, amount))
        {
            TempData["Error"] = CashBoxHelper.FormatInsufficientBalanceMessage(cashBox, opCurrency, amount);
            return RedirectToAction(nameof(Index));
        }

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يرجى تسجيل الدخول.";
            return RedirectToAction(nameof(Index));
        }

        decimal balanceBefore = CashBoxHelper.GetBalance(cashBox, opCurrency);
        CashBoxHelper.ApplyDelta(cashBox, opCurrency, -amount);

        CashBoxWithdrawal withdrawal = new()
        {
            CashBoxId = cashBox.Id,
            Amount = amount,
            Currency = opCurrency,
            WithdrawnAt = DateTime.Now,
            WithdrawnByUserId = user.Id,
            Notes = notes?.Trim(),
            BalanceBefore = balanceBefore,
            BalanceAfter = CashBoxHelper.GetBalance(cashBox, opCurrency)
        };
        _context.CashBoxWithdrawals.Add(withdrawal);
        await _context.SaveChangesAsync();

        _logger.LogInformation("سحب من صندوق الشبكة #{CashBoxId} {Amount} {Currency} من قبل {UserId}",
            cashBox.Id, amount, opCurrency, user.Id);
        TempData["Success"] =
            $"تم سحب {CurrencyHelper.FormatAmount(amount, opCurrency)} من الصندوق. الرصيد الحالي: {CurrencyHelper.FormatAmount(withdrawal.BalanceAfter, opCurrency)}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Deposit(decimal amount, int currency, string? notes)
    {
        (int? effectiveNetworkId, string? effectiveNetworkName)? networkInfo = await GetEffectiveNetworkAsync();
        if (!networkInfo.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        PricingCurrency opCurrency = ParseCurrency(currency);
        (int? effectiveNetworkId, string? _) = networkInfo.Value;
        CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.Network, effectiveNetworkId!.Value);
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

        ApplicationUser? user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["Error"] = "يرجى تسجيل الدخول.";
            return RedirectToAction(nameof(Index));
        }

        decimal balanceBefore = CashBoxHelper.GetBalance(cashBox, opCurrency);
        CashBoxHelper.ApplyDelta(cashBox, opCurrency, amount);

        CashBoxDeposit deposit = new()
        {
            CashBoxId = cashBox.Id,
            Amount = amount,
            Currency = opCurrency,
            DepositedAt = DateTime.Now,
            DepositedByUserId = user.Id,
            Notes = notes?.Trim(),
            BalanceBefore = balanceBefore,
            BalanceAfter = CashBoxHelper.GetBalance(cashBox, opCurrency)
        };
        _context.CashBoxDeposits.Add(deposit);
        await _context.SaveChangesAsync();

        _logger.LogInformation("إيداع في صندوق الشبكة #{CashBoxId} {Amount} {Currency} من قبل {UserId}",
            cashBox.Id, amount, opCurrency, user.Id);
        TempData["Success"] =
            $"تم إيداع {CurrencyHelper.FormatAmount(amount, opCurrency)} في الصندوق. الرصيد الحالي: {CurrencyHelper.FormatAmount(deposit.BalanceAfter, opCurrency)}";
        return RedirectToAction(nameof(Index));
    }
}
