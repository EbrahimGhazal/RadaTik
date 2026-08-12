using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Helpers;
using global::RadaTik.Models;
using global::RadaTik.Security;

namespace RadaTik.Areas.SystemAdmin.Controllers;

[Area("SystemAdmin")]
[Authorize(Roles = RoleNames.SystemAdministrator)]
public class CollectionPointTopUpRequestsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<CollectionPointTopUpRequestsController> _logger;

    public CollectionPointTopUpRequestsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<CollectionPointTopUpRequestsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// طلبات تغذية رصيد نقاط التحصيل الموجهة لمدير النظام
    /// </summary>
    [HttpGet]
    public IActionResult Index(CollectionPointTopUpStatus? status = null)
    {
        // Backward compatibility: redirect to the consolidated page
        return RedirectToAction(
            "Index",
            "FundingRequests",
            new { area = "SystemAdmin", tab = "collectionPoints", collectionPointStatus = status });
    }

    [HttpGet]
    public async Task<IActionResult> LegacyIndex(CollectionPointTopUpStatus? status = null)
    {
        ViewData["Title"] = "طلبات تغذية رصيد نقاط التحصيل (مدير النظام)";

        IQueryable<CollectionPointTopUpRequest> query = _context.CollectionPointTopUpRequests
            .Include(r => r.CollectionPointAccount)
                .ThenInclude(a => a!.User)
            .Include(r => r.CollectionPointAccount)
                .ThenInclude(a => a!.Network)
            .Include(r => r.PaymentMethod)
            .Include(r => r.RequestedByUser)
            .Include(r => r.TargetNetwork)
            .Where(r => r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin)
            .AsQueryable();

        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        List<CollectionPointTopUpRequest> list = await query.OrderByDescending(r => r.RequestedAt).Take(200).ToListAsync();
        ViewBag.Items = list;
        ViewBag.SelectedStatus = status;
        ViewBag.PendingCount = await _context.CollectionPointTopUpRequests
            .CountAsync(r => r.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin && r.Status == CollectionPointTopUpStatus.Pending);

        return View("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? adminNotes = null)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        try
        {
            await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();

            CollectionPointTopUpRequest? req = await _context.CollectionPointTopUpRequests
                .Include(r => r.PaymentMethod)
                .Include(r => r.CollectionPointAccount).ThenInclude(a => a!.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (req == null)
            {
                return NotFound();
            }

            if (req.RequestTargetType != CollectionPointTopUpTarget.SystemAdmin)
            {
                TempData["Error"] = "هذا الطلب موجه لمدير الشركة وليس لمدير النظام.";
                return RedirectToAction(nameof(Index));
            }

            if (req.Status != CollectionPointTopUpStatus.Pending)
            {
                TempData["Error"] = "لا يمكن الموافقة على طلب غير معلّق.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(req.ReferenceNumber) || string.IsNullOrWhiteSpace(req.ReceiptImagePath))
            {
                TempData["Error"] = "لا يمكن الموافقة: الطلب ناقص بيانات الإيصال (رقم المرجع + صورة الإيصال).";
                return RedirectToAction(nameof(Index));
            }

            DateTime now = DateTime.Now;
            CollectionPointAccount account = req.CollectionPointAccount!;
            account.Balance += req.Amount;
            account.UpdatedAt = now;

            req.Status = CollectionPointTopUpStatus.Approved;
            req.ProcessedByUserId = currentUser.Id;
            req.ProcessedAt = now;
            req.AdminNotes = adminNotes?.Trim();

            await _context.SaveChangesAsync();

            // إذا كانت طريقة الدفع "كاش" -> إيداع تلقائي في خزنة مدير النظام (نقد باليد)
            if (req.PaymentMethodId.HasValue && req.PaymentMethod?.IsCash == true)
            {
                CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.SystemAdmin, 0);
                if (cashBox != null)
                {
                    bool alreadyDeposited = await _context.CashBoxDeposits
                        .AnyAsync(d => d.CollectionPointTopUpRequestId == req.Id);
                    if (!alreadyDeposited)
                    {
                        decimal balanceBeforeCash = cashBox.Balance;
                        cashBox.Balance = balanceBeforeCash + req.Amount;
                        cashBox.UpdatedAt = now;

                        _context.CashBoxDeposits.Add(new CashBoxDeposit
                        {
                            CashBoxId = cashBox.Id,
                            Amount = req.Amount,
                            DepositedAt = now,
                            DepositedByUserId = currentUser.Id,
                            PaymentMethodId = req.PaymentMethodId,
                            CollectionPointTopUpRequestId = req.Id,
                            Notes = $"قبض كاش مقابل تغذية رصيد نقطة تحصيل: {account.User?.UserName ?? account.UserId} (طلب #{req.Id}) — مرجع: {req.ReferenceNumber}",
                            BalanceBefore = balanceBeforeCash,
                            BalanceAfter = cashBox.Balance
                        });

                        await _context.SaveChangesAsync();
                    }
                }
            }

            await tx.CommitAsync();

            _logger.LogInformation("مدير النظام وافق على طلب تغذية رصيد #{Id} لنقطة التحصيل {UserName} بمبلغ {Amount}",
                id, account.User?.UserName, req.Amount);

            TempData["Success"] = $"تمت الموافقة على الطلب وإضافة {req.Amount:N0} ل.س إلى رصيد نقطة التحصيل.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to approve collection point top-up request.");
            TempData["Error"] = "تعذر تنفيذ الموافقة.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? adminNotes = null)
    {
        ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
        if (currentUser == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        CollectionPointTopUpRequest? req = await _context.CollectionPointTopUpRequests
            .Include(r => r.CollectionPointAccount)
            .FirstOrDefaultAsync(r => r.Id == id);

        if (req == null)
        {
            return NotFound();
        }

        if (req.RequestTargetType != CollectionPointTopUpTarget.SystemAdmin)
        {
            TempData["Error"] = "هذا الطلب موجه لمدير الشركة وليس لمدير النظام.";
            return RedirectToAction(nameof(Index));
        }

        if (req.Status != CollectionPointTopUpStatus.Pending)
        {
            TempData["Error"] = "لا يمكن رفض طلب غير معلّق.";
            return RedirectToAction(nameof(Index));
        }

        req.Status = CollectionPointTopUpStatus.Rejected;
        req.ProcessedByUserId = currentUser.Id;
        req.ProcessedAt = DateTime.Now;
        req.AdminNotes = adminNotes?.Trim();

        await _context.SaveChangesAsync();

        TempData["Success"] = AppMessages.OperationSuccess;
        return RedirectToAction(nameof(Index));
    }
}
