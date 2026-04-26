using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;
using RadTik.Helpers;
using RadTik.Services;
using RadTik.Services.PricingPolicies;

namespace RadTik.Areas.SystemAdmin.Controllers
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

            var query = _context.NetworkServiceRequests
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

            var list = await query
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
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var req = await _context.NetworkServiceRequests
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

                var now = DateTime.Now;

                var senderApproval = await _senderPricingOrchestrator.TryHandlePendingApprovalAsync(req, admin.Id, notes);
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

                var sub = await _context.NetworkServiceSubscriptions
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
                    var baseDate = sub.ExpiresAt > now ? sub.ExpiresAt : now;
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

                TempData["Success"] = "تمت الموافقة على طلب الخدمة وتفعيل الاشتراك.";
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
            var admin = await _userManager.GetUserAsync(User);
            if (admin == null)
            {
                return RedirectToAction("Login", "Account", new { area = "" });
            }

            try
            {
                await using var tx = await _context.Database.BeginTransactionAsync();

                var req = await _context.NetworkServiceRequests
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

                var now = DateTime.Now;

                await _senderPricingOrchestrator.TryHandlePendingRejectionAsync(req);

                req.Status = NetworkServiceRequestStatus.Rejected;
                req.DecidedByUserId = admin.Id;
                req.DecidedAt = now;
                req.Notes = string.IsNullOrWhiteSpace(notes) ? req.Notes : notes.Trim();

                await _context.SaveChangesAsync();

                await tx.CommitAsync();
                TempData["Success"] = "تم رفض الطلب.";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to reject service request.");
                TempData["Error"] = "تعذر تنفيذ الرفض.";
            }

            return RedirectToAction(nameof(Index), new { status = NetworkServiceRequestStatus.Pending });
        }


    }
}

