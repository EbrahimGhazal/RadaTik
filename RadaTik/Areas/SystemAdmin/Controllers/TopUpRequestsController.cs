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
using global::RadaTik.Services;

namespace RadaTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class TopUpRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TopUpRequestsController> _logger;
        private readonly IWalletTopUpSubscriptionResumeService _walletTopUpSubscriptionResume;
        private readonly CompanyWalletCashTransferService _companyCashTransferService;

        public TopUpRequestsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<TopUpRequestsController> logger,
            IWalletTopUpSubscriptionResumeService walletTopUpSubscriptionResume,
            CompanyWalletCashTransferService companyCashTransferService)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _walletTopUpSubscriptionResume = walletTopUpSubscriptionResume;
            _companyCashTransferService = companyCashTransferService;
        }

        [HttpGet]
        public IActionResult Index(NetworkTopUpRequestStatus? status = null)
        {
            // Backward compatibility: redirect to the consolidated page
            return RedirectToFundingRequestsIndex("companies", companyStatus: status);
        }

        [HttpGet]
        public async Task<IActionResult> LegacyIndex(NetworkTopUpRequestStatus? status = null)
        {
            ViewData["Title"] = "طلبات تغذية رصيد الشركات";

            IQueryable<NetworkTopUpRequest> query = _context.NetworkTopUpRequests
                .Include(r => r.Network)
                .Include(r => r.PaymentMethod)
                .Include(r => r.RequestedByUser)
                .Include(r => r.DecidedByUser)
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            List<NetworkTopUpRequest> list = await query
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
            ApplicationUser? admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            NetworkTopUpRequest? req = await _context.NetworkTopUpRequests
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

            Network? company = await _context.Networks.FirstOrDefaultAsync(n => n.Id == req.NetworkId);
            if (company == null)
            {
                TempData["Error"] = "تعذر العثور على الشركة.";
                return RedirectToAction(nameof(Index));
            }

            await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
            try
            {
                DateTime now = DateTime.Now;
                decimal amount = req.Amount;

                if (req.DeductFromCompanyCashBoxOnApproval)
                {
                    req.DeductFromCompanyCashBoxOnApproval = false;
                }

                decimal previousBalance = company.Balance;
                company.Balance = previousBalance + amount;

                req.Status = NetworkTopUpRequestStatus.Approved;
                req.DecidedByUserId = admin.Id;
                req.DecidedAt = now;
                if (!string.IsNullOrWhiteSpace(notes))
                {
                    req.Notes = notes.Trim();
                }

                await _context.SaveChangesAsync();

                string walletNote = $"موافقة تغذية رصيد (طلب #{req.Id})";

                NetworkWalletTransaction walletTx = new NetworkWalletTransaction
                {
                    NetworkId = req.NetworkId,
                    Type = NetworkWalletTransactionType.TopUp,
                    Currency = PricingCurrency.SYP_New,
                    SignedAmount = amount,
                    PreviousBalance = previousBalance,
                    NewBalance = company.Balance,
                    NetworkTopUpRequestId = req.Id,
                    CreatedByUserId = admin.Id,
                    CreatedAt = now,
                    Notes = walletNote
                };
                _context.NetworkWalletTransactions.Add(walletTx);
                await _context.SaveChangesAsync();

                req.ApprovedWalletTransactionId = walletTx.Id;
                await _context.SaveChangesAsync();

                // إذا كانت طريقة الدفع "كاش" -> إيداع تلقائي في خزنة مدير النظام (نقد باليد)
                if (req.PaymentMethodId.HasValue && req.PaymentMethod?.IsCash == true)
                {
                    CashBox? cashBox = await CashBoxHelper.GetOrCreateCashBoxAsync(_context, CashBoxOwnerType.SystemAdmin, 0);
                    if (cashBox != null)
                    {
                        bool alreadyDeposited = await _context.CashBoxDeposits
                            .AnyAsync(d => d.NetworkTopUpRequestId == req.Id);
                        if (!alreadyDeposited)
                        {
                            decimal balanceBeforeCash = cashBox.Balance;
                            cashBox.Balance = balanceBeforeCash + amount;
                            cashBox.UpdatedAt = now;

                            _context.CashBoxDeposits.Add(new CashBoxDeposit
                            {
                                CashBoxId = cashBox.Id,
                                Amount = amount,
                                Currency = PricingCurrency.SYP_New,
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

                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "Failed to approve top-up request {RequestId}.", id);
                TempData["Error"] = BuildApprovalErrorMessage(ex);
            }

            return RedirectToFundingRequestsIndex("companies", companyStatus: NetworkTopUpRequestStatus.Pending);
        }

        /// <summary>
        /// يمنع تسرّب action الحالي (مثل Approve) إلى مسار fundingRequests/{action}.
        /// </summary>
        private IActionResult RedirectToFundingRequestsIndex(
            string tab,
            NetworkTopUpRequestStatus? companyStatus = null,
            CollectionPointTopUpStatus? collectionPointStatus = null) =>
            RedirectToAction(
                "Index",
                "FundingRequests",
                new
                {
                    area = "SystemAdmin",
                    tab,
                    companyStatus,
                    collectionPointStatus
                });

        private static string BuildApprovalErrorMessage(Exception ex)
        {
            Exception? root = ex;
            while (root.InnerException != null)
            {
                root = root.InnerException;
            }

            string detail = root.Message;
            if (detail.Contains("Currency", StringComparison.OrdinalIgnoreCase)
                || detail.Contains("Invalid column name", StringComparison.OrdinalIgnoreCase))
            {
                return "تعذر تنفيذ الموافقة: مخطط قاعدة البيانات ناقص. شغّل «dotnet ef database update» من مجلد المشروع ثم أعد المحاولة.";
            }

            return "تعذر تنفيذ الموافقة. تأكد من اكتمال ترحيلات قاعدة البيانات ثم أعد المحاولة.";
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? notes = null)
        {
            ApplicationUser? admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            try
            {
                NetworkTopUpRequest? req = await _context.NetworkTopUpRequests.FirstOrDefaultAsync(r => r.Id == id);
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
                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reject top-up request.");
                TempData["Error"] = "تعذر تنفيذ الرفض.";
            }

            return RedirectToFundingRequestsIndex("companies", companyStatus: NetworkTopUpRequestStatus.Pending);
        }
    }
}

