using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;

namespace RadTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class TopUpRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TopUpRequestsController> _logger;
        private readonly IWalletTopUpSubscriptionResumeService _walletTopUpSubscriptionResume;

        public TopUpRequestsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<TopUpRequestsController> logger,
            IWalletTopUpSubscriptionResumeService walletTopUpSubscriptionResume)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _walletTopUpSubscriptionResume = walletTopUpSubscriptionResume;
        }

        [HttpGet]
        public IActionResult Index(NetworkTopUpRequestStatus? status = null)
        {
            // Backward compatibility: redirect to the consolidated page
            return RedirectToRoute("systemAdmin-fundingRequests", new { tab = "companies", companyStatus = status });
        }

        [HttpGet]
        public async Task<IActionResult> LegacyIndex(NetworkTopUpRequestStatus? status = null)
        {
            ViewData["Title"] = "طلبات تغذية رصيد الشركات";

            var query = _context.NetworkTopUpRequests
                .Include(r => r.Network)
                .Include(r => r.PaymentMethod)
                .Include(r => r.RequestedByUser)
                .Include(r => r.DecidedByUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            var list = await query
                .OrderByDescending(r => r.RequestedAt)
                .Take(500)
                .ToListAsync();

            ViewBag.Items = list;
            ViewBag.SelectedStatus = status;
            ViewBag.PendingCount = await _context.NetworkTopUpRequests.CountAsync(r => r.Status == NetworkTopUpRequestStatus.Pending);

            return View("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id, string? notes = null)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var req = await _context.NetworkTopUpRequests
                    .Include(r => r.PaymentMethod)
                    .Include(r => r.Network)
                    .FirstOrDefaultAsync(r => r.Id == id);
                if (req == null)
                {
                    return NotFound();
                }

                if (req.Status != NetworkTopUpRequestStatus.Pending)
                {
                    TempData["Error"] = "لا يمكن الموافقة على طلب غير معلّق.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.IsNullOrWhiteSpace(req.ReferenceNumber) || string.IsNullOrWhiteSpace(req.ReceiptImagePath))
                {
                    TempData["Error"] = "لا يمكن الموافقة: الطلب ناقص بيانات الإيصال (رقم المرجع + صورة الإيصال).";
                    return RedirectToAction(nameof(Index));
                }

                var company = await _context.Networks.FirstOrDefaultAsync(n => n.Id == req.NetworkId);
                if (company == null)
                {
                    TempData["Error"] = "تعذر العثور على الشركة.";
                    return RedirectToAction(nameof(Index));
                }

                var now = DateTime.Now;
                var amount = req.Amount;

                var previousBalance = company.Balance;
                company.Balance = previousBalance + amount;

                req.Status = NetworkTopUpRequestStatus.Approved;
                req.DecidedByUserId = admin.Id;
                req.DecidedAt = now;
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    req.Notes = notes.Trim();
                }

                await _context.SaveChangesAsync();

                var walletTx = new NetworkWalletTransaction
                {
                    NetworkId = req.NetworkId,
                    Type = NetworkWalletTransactionType.TopUp,
                    SignedAmount = amount,
                    PreviousBalance = previousBalance,
                    NewBalance = company.Balance,
                    NetworkTopUpRequestId = req.Id,
                    CreatedByUserId = admin.Id,
                    CreatedAt = now,
                    Notes = $"موافقة تغذية رصيد (طلب #{req.Id})"
                };
                _context.NetworkWalletTransactions.Add(walletTx);
                await _context.SaveChangesAsync();

                req.ApprovedWalletTransactionId = walletTx.Id;
                await _context.SaveChangesAsync();

                // إذا كانت طريقة الدفع "كاش" -> إيداع تلقائي في خزنة مدير النظام (نقد باليد)
                if (req.PaymentMethodId.HasValue && req.PaymentMethod?.IsCash == true)
                {
                    var cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.SystemAdmin, 0);
                    if (cashBox != null)
                    {
                        var alreadyDeposited = await _context.CashBoxDeposits
                            .AnyAsync(d => d.NetworkTopUpRequestId == req.Id);
                        if (!alreadyDeposited)
                        {
                            var balanceBeforeCash = cashBox.Balance;
                            cashBox.Balance = balanceBeforeCash + amount;
                            cashBox.UpdatedAt = now;

                            _context.CashBoxDeposits.Add(new CashBoxDeposit
                            {
                                CashBoxId = cashBox.Id,
                                Amount = amount,
                                DepositedAt = now,
                                DepositedByUserId = admin.Id,
                                PaymentMethodId = req.PaymentMethodId,
                                NetworkTopUpRequestId = req.Id,
                                Notes = $"قبض كاش مقابل تغذية رصيد شركة: {req.Network?.Name ?? ("#" + req.NetworkId)} (طلب #{req.Id}) — مرجع: {req.ReferenceNumber}",
                                BalanceBefore = balanceBeforeCash,
                                BalanceAfter = cashBox.Balance
                            });

                            await _context.SaveChangesAsync();
                        }
                    }
                }

                await tx.CommitAsync();

                try
                {
                    await _walletTopUpSubscriptionResume.ResumeAfterCompanyWalletTopUpAsync(req.NetworkId);
                }
                catch (Exception resumeEx)
                {
                    _logger.LogWarning(resumeEx, "Resume subscriptions after wallet top-up failed for network {NetworkId}", req.NetworkId);
                }

                TempData["Success"] = "تمت الموافقة على طلب تغذية الرصيد وإضافة المبلغ.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to approve top-up request.");
                TempData["Error"] = "تعذر تنفيذ الموافقة.";
            }

            return RedirectToAction(nameof(Index), new { status = NetworkTopUpRequestStatus.Pending });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? notes = null)
        {
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            try
            {
                var req = await _context.NetworkTopUpRequests.FirstOrDefaultAsync(r => r.Id == id);
                if (req == null)
                {
                    return NotFound();
                }

                if (req.Status != NetworkTopUpRequestStatus.Pending)
                {
                    TempData["Error"] = "لا يمكن رفض طلب غير معلّق.";
                    return RedirectToAction(nameof(Index));
                }

                req.Status = NetworkTopUpRequestStatus.Rejected;
                req.DecidedByUserId = admin.Id;
                req.DecidedAt = DateTime.Now;
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    req.Notes = notes.Trim();
                }

                await _context.SaveChangesAsync();
                TempData["Success"] = "تم رفض طلب تغذية الرصيد.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reject top-up request.");
                TempData["Error"] = "تعذر تنفيذ الرفض.";
            }

            return RedirectToAction(nameof(Index), new { status = NetworkTopUpRequestStatus.Pending });
        }
    }
}

