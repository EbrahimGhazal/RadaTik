using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using global::RadaTik.Constants;
using global::RadaTik.Data;
using global::RadaTik.Models;
using global::RadaTik.Security;
using global::RadaTik.Helpers;
using global::RadaTik.Services;
using global::RadaTik.Services.PricingPolicies;
using Microsoft.EntityFrameworkCore.Storage;

namespace RadaTik.Areas.SystemAdmin.Controllers
{
    [Area("SystemAdmin")]
    [Authorize(Roles = RoleNames.SystemAdministrator)]
    public class ServiceRequestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ServiceRequestsController> _logger;
        private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
        private readonly ISenderPricingOrchestrator _senderPricingOrchestrator;

        public ServiceRequestsController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            ILogger<ServiceRequestsController> logger,
            IUsageBasedSubscriptionChargeService usageChargeService,
            ISenderPricingOrchestrator senderPricingOrchestrator)
        {
            _context = context;
            _userManager = userManager;
            _logger = logger;
            _usageChargeService = usageChargeService;
            _senderPricingOrchestrator = senderPricingOrchestrator;
        }

        [HttpGet]
        public async Task<IActionResult> Index(NetworkServiceRequestStatus? status = null)
        {
            ViewData["Title"] = "طلبات خدمات الشركات";

            IQueryable<NetworkServiceRequest> query = _context.NetworkServiceRequests
                .Include(r => r.Network)
                .Include(r => r.RequestedByUser)
                .Include(r => r.DecidedByUser)
                .Where(r =>
                    (r.Notes == null || !r.Notes.StartsWith("EMP_REQ:")) &&
                    !(r.FeatureKey == FeatureKeys.Sectors && r.Notes != null && r.Notes.Contains("SECTOR_CREATE_PENDING:")))
                .AsQueryable();

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            List<NetworkServiceRequest> list = await query
                .OrderByDescending(r => r.RequestedAt)
                .Take(500)
                .ToListAsync();

            ViewBag.Items = list;
            ViewBag.SelectedStatus = status;
            ViewBag.PendingCount = await _context.NetworkServiceRequests.CountAsync(r => r.Status == NetworkServiceRequestStatus.Pending);

            return View();
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

            try
            {
                await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();

                NetworkServiceRequest? req = await _context.NetworkServiceRequests
                    .Include(r => r.Network)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (req == null)
                {
                    return NotFound();
                }

                if (req.Status != NetworkServiceRequestStatus.Pending)
                {
                    TempData["Error"] = "لا يمكن الموافقة على طلب غير معلّق.";
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrWhiteSpace(req.Notes) && req.Notes.StartsWith("EMP_REQ:", StringComparison.Ordinal))
                {
                    TempData["Error"] = "هذا الطلب يتبع موافقات مدير الشركة ويجب اعتماده من واجهة مدير الشركة.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.Equals(req.FeatureKey, FeatureKeys.Sectors, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(req.Notes) &&
                    req.Notes.Contains("SECTOR_CREATE_PENDING:", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "طلب إضافة المرسل من الموظف يجب اعتماده من واجهة مدير الشركة.";
                    return RedirectToAction(nameof(Index));
                }

                DateTime now = DateTime.Now;

                SenderApprovalOutcome senderApproval = await _senderPricingOrchestrator.TryHandlePendingApprovalAsync(req, admin.Id, notes);
                if (senderApproval.Handled)
                {
                    if (senderApproval.OutcomeType != SenderApprovalOutcomeType.ApprovedAndCharged)
                    {
                        TempData["Error"] = senderApproval.Message;
                        return RedirectToAction(nameof(Index));
                    }

                    await tx.CommitAsync();
                    TempData["Success"] = senderApproval.Message;
                    return RedirectToAction(nameof(Index), new { status = NetworkServiceRequestStatus.Pending });
                }

                NetworkServiceSubscription? sub = await _context.NetworkServiceSubscriptions
                    .FirstOrDefaultAsync(s => s.NetworkId == req.NetworkId && s.FeatureKey == req.FeatureKey);

                if (sub == null)
                {
                    sub = new NetworkServiceSubscription
                    {
                        NetworkId = req.NetworkId,
                        FeatureKey = req.FeatureKey,
                        BillingPeriod = req.BillingPeriod,
                        StartAt = now,
                        ExpiresAt = BillingPeriodDateCalculator.AddPeriod(now, req.BillingPeriod),
                        Status = NetworkServiceSubscriptionStatus.Active,
                        CreatedAt = now,
                        UpdatedAt = now,
                        LastApprovedRequestId = req.Id
                    };
                    _context.NetworkServiceSubscriptions.Add(sub);
                    await _context.SaveChangesAsync();
                    await _usageChargeService.InitializeBaselineAsync(req.NetworkId, sub.Id);
                }
                else
                {
                    // Extend from current expiry if still valid, otherwise from now.
                    DateTime baseDate = sub.ExpiresAt > now ? sub.ExpiresAt : now;
                    sub.BillingPeriod = req.BillingPeriod;
                    sub.Status = NetworkServiceSubscriptionStatus.Active;
                    sub.StartAt = sub.StartAt == default ? now : sub.StartAt;
                    sub.ExpiresAt = BillingPeriodDateCalculator.AddPeriod(baseDate, req.BillingPeriod);
                    sub.UpdatedAt = now;
                    sub.LastApprovedRequestId = req.Id;
                    await _context.SaveChangesAsync();
                    await _usageChargeService.InitializeBaselineAsync(req.NetworkId, sub.Id);
                }

                req.Status = NetworkServiceRequestStatus.Approved;
                req.DecidedByUserId = admin.Id;
                req.DecidedAt = now;
                req.Notes = string.IsNullOrWhiteSpace(notes) ? req.Notes : notes.Trim();

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to approve service request.");
                TempData["Error"] = "تعذر تنفيذ الموافقة.";
            }

            return RedirectToAction(nameof(Index), new { status = NetworkServiceRequestStatus.Pending });
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
                await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();

                NetworkServiceRequest? req = await _context.NetworkServiceRequests
                    .Include(r => r.Network)
                    .FirstOrDefaultAsync(r => r.Id == id);

                if (req == null)
                {
                    return NotFound();
                }

                if (req.Status != NetworkServiceRequestStatus.Pending)
                {
                    TempData["Error"] = "لا يمكن رفض طلب غير معلّق.";
                    return RedirectToAction(nameof(Index));
                }

                if (!string.IsNullOrWhiteSpace(req.Notes) && req.Notes.StartsWith("EMP_REQ:", StringComparison.Ordinal))
                {
                    TempData["Error"] = "هذا الطلب يتبع موافقات مدير الشركة ويجب رفضه من واجهة مدير الشركة.";
                    return RedirectToAction(nameof(Index));
                }

                if (string.Equals(req.FeatureKey, FeatureKeys.Sectors, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(req.Notes) &&
                    req.Notes.Contains("SECTOR_CREATE_PENDING:", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Error"] = "طلب إضافة المرسل من الموظف يجب رفضه من واجهة مدير الشركة.";
                    return RedirectToAction(nameof(Index));
                }

                DateTime now = DateTime.Now;

                await _senderPricingOrchestrator.TryHandlePendingRejectionAsync(req);

                await TryRefundServiceRequestChargeAsync(req, admin.Id, now);

                req.Status = NetworkServiceRequestStatus.Rejected;
                req.DecidedByUserId = admin.Id;
                req.DecidedAt = now;
                req.Notes = string.IsNullOrWhiteSpace(notes) ? req.Notes : notes.Trim();

                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reject service request.");
                TempData["Error"] = "تعذر تنفيذ الرفض.";
            }

            return RedirectToAction(nameof(Index), new { status = NetworkServiceRequestStatus.Pending });
        }

        private async Task TryRefundServiceRequestChargeAsync(
            NetworkServiceRequest req,
            string adminUserId,
            DateTime now)
        {
            if (req.RefundWalletTransactionId.HasValue || req.AmountSYP <= 0m || !req.ChargeWalletTransactionId.HasValue)
            {
                return;
            }

            Network? company = await _context.Networks
                .FirstOrDefaultAsync(n => n.Id == req.NetworkId && n.ParentNetworkId == null)
                ?? await _context.Networks.FirstOrDefaultAsync(n => n.Id == req.NetworkId);

            if (company == null)
            {
                return;
            }

            decimal refundAmount = WalletMath.CeilSyp(req.AmountSYP);
            decimal previousBalance = company.Balance;
            company.Balance += refundAmount;

            NetworkWalletTransaction refundTx = new NetworkWalletTransaction
            {
                NetworkId = req.NetworkId,
                Type = NetworkWalletTransactionType.Refund,
                SignedAmount = refundAmount,
                PreviousBalance = previousBalance,
                NewBalance = company.Balance,
                CreatedByUserId = adminUserId,
                CreatedAt = now,
                Notes = $"استرجاع طلب خدمة #{req.Id} ({req.FeatureKey})"
            };
            _context.NetworkWalletTransactions.Add(refundTx);
            await _context.SaveChangesAsync();
            req.RefundWalletTransactionId = refundTx.Id;
        }
    }
}

