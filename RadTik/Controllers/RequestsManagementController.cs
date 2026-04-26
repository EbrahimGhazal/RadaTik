using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Services;
using RadTik.Security;
using RadTik.Helpers;
using RadTik.ViewModels.Maintenance;

namespace RadTik.Controllers
{
    /// <summary>
    /// إدارة طلبات الصيانة وتغيير السرعة - للمدير والموظفين
    /// </summary>
    // CompanyEmployee هو الدور الجديد للموظف التابع للشركة، و EmployeeLegacy للتوافق.
    [Authorize(Roles = RoleNames.NetworkAdministrator + "," + RoleNames.CompanyEmployee + "," + RoleNames.EmployeeLegacy)]
    [Authorize(Policy = FeaturePolicyProvider.PolicyPrefix + FeatureKeys.Requests)]
    [RequirePermission("Requests.View")]
    public class RequestsManagementController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IMikroTikUsersService _mikroTikService;
        private readonly PermissionService _permissionService;
        private readonly IMaintenanceBillingService _maintenanceBillingService;
        private readonly ILogger<RequestsManagementController> _logger;

        public RequestsManagementController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMikroTikUsersService mikroTikService,
            PermissionService permissionService,
            IMaintenanceBillingService maintenanceBillingService,
            ILogger<RequestsManagementController> logger)
        {
            _context = context;
            _userManager = userManager;
            _mikroTikService = mikroTikService;
            _permissionService = permissionService;
            _maintenanceBillingService = maintenanceBillingService;
            _logger = logger;
        }

        /// <summary>
        /// الموافقة أو الرفض على تغيير السرعة: مدير الشبكة أو SpeedChange.Approve (أو Implement للتوافق مع الإسناد القديم).
        /// </summary>
        private Task<bool> CanApproveOrRejectSpeedChangeAsync() =>
            _permissionService.HasPermissionAsync(User, "SpeedChange.Approve");

        /// <summary>
        /// تنفيذ تغيير السرعة على المايكروتك: مدير الشبكة أو SpeedChange.Implement فقط.
        /// </summary>
        private Task<bool> CanImplementSpeedChangeAsync() =>
            _permissionService.HasPermissionAsync(User, "SpeedChange.Implement");

        private string ResolveRequestsRouteName()
        {
            var currentArea = RouteData.Values["area"]?.ToString();
            return string.Equals(currentArea, "CompanyEmployee", StringComparison.OrdinalIgnoreCase)
                ? "employee-requestsManagement"
                : "networkManager-requestsManagement";
        }

        private IActionResult RedirectToRequestsAction(string action, object? routeValues = null)
        {
            return RedirectToRoute(ResolveRequestsRouteName(), new Microsoft.AspNetCore.Routing.RouteValueDictionary(routeValues ?? new { }) { ["action"] = action });
        }

        #region لوحة التحكم

        /// <summary>
        /// لوحة تحكم الطلبات - عرض الطلبات المعلقة
        /// </summary>
        public async Task<IActionResult> Index()
        {
            // إحصائيات طلبات الصيانة
            ViewBag.PendingMaintenanceCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.Pending);
            ViewBag.InProgressMaintenanceCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.InProgress || m.Status == MaintenanceRequestStatus.Accepted);
            ViewBag.CompletedMaintenanceCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.Completed);

            // إحصائيات طلبات تغيير السرعة
            ViewBag.PendingSpeedChangeCount = await _context.SpeedChangeRequests
                .CountAsync(s => s.Status == SpeedChangeRequestStatus.Pending);
            ViewBag.ApprovedSpeedChangeCount = await _context.SpeedChangeRequests
                .CountAsync(s => s.Status == SpeedChangeRequestStatus.Approved);
            ViewBag.ImplementedSpeedChangeCount = await _context.SpeedChangeRequests
                .CountAsync(s => s.Status == SpeedChangeRequestStatus.Implemented);

            // آخر الطلبات المعلقة
            ViewBag.RecentMaintenanceRequests = await _context.MaintenanceRequests
                .Include(m => m.Client)
                .Where(m => m.Status == MaintenanceRequestStatus.Pending)
                .OrderByDescending(m => m.RequestDate)
                .Take(5)
                .ToListAsync();

            ViewBag.RecentSpeedChangeRequests = await _context.SpeedChangeRequests
                .Include(s => s.Client)
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Where(s => s.Status == SpeedChangeRequestStatus.Pending)
                .OrderByDescending(s => s.RequestDate)
                .Take(5)
                .ToListAsync();

            return View();
        }

        /// <summary>
        /// الحصول على عدد الطلبات المعلقة (للإشعارات)
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetPendingRequestsCount()
        {
            var maintenanceCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.Pending || 
                                m.Status == MaintenanceRequestStatus.Accepted ||
                                m.Status == MaintenanceRequestStatus.InProgress);

            var speedChangeCount = await _context.SpeedChangeRequests
                .CountAsync(s => s.Status == SpeedChangeRequestStatus.Pending ||
                                s.Status == SpeedChangeRequestStatus.Approved);

            return Json(new
            {
                maintenance = maintenanceCount,
                speedChange = speedChangeCount,
                total = maintenanceCount + speedChangeCount
            });
        }

        #endregion

        #region إدارة طلبات الصيانة

        /// <summary>
        /// قائمة طلبات الصيانة
        /// </summary>
        public async Task<IActionResult> MaintenanceRequests(string? status = null)
        {
            var pendingRequestsCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.Pending);

            var query = _context.MaintenanceRequests
                .Include(m => m.Client)
                .Include(m => m.AssignedTo)
                .AsQueryable();

            // تصفية حسب الحالة
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<MaintenanceRequestStatus>(status, out var statusEnum))
                {
                    query = query.Where(m => m.Status == statusEnum);
                }
            }

            var requests = await query
                .OrderByDescending(m => m.RequestDate)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            ViewBag.PendingRequestsCount = pendingRequestsCount;
            return View(requests);
        }

        /// <summary>
        /// تفاصيل طلب صيانة
        /// </summary>
        public async Task<IActionResult> MaintenanceRequestDetails(int id)
        {
            var request = await _context.MaintenanceRequests
                .Include(m => m.Client)
                    .ThenInclude(c => c!.Profile)
                .Include(m => m.Client)
                    .ThenInclude(c => c!.Receiver)
                .Include(m => m.AssignedTo)
                .Include(m => m.ProcessedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            // جلب قائمة الموظفين للتعيين
            var companyEmployees = await _userManager.GetUsersInRoleAsync(RoleNames.CompanyEmployee);
            var legacyEmployees = await _userManager.GetUsersInRoleAsync(RoleNames.EmployeeLegacy);
            var admins = await _userManager.GetUsersInRoleAsync(RoleNames.NetworkAdministrator);
            ViewBag.AvailableStaff = companyEmployees.Concat(legacyEmployees).Concat(admins).Distinct().ToList();
            ViewBag.MaintenanceInvoice = await _context.MaintenanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.MaintenanceRequestId == request.Id);
            ViewBag.PricedMaintenanceOptions = await LoadPricedMaintenanceOptionsAsync(request);
            ViewBag.MaintenanceTransportFee = await LoadMaintenanceTransportFeeAsync(request);
            var commissionPreview = await LoadMaintenanceCommissionPreviewAsync(request);
            ViewBag.MaintenanceCommissionMode = commissionPreview.Mode;
            ViewBag.MaintenanceCommissionValue = commissionPreview.Value;

            return View(request);
        }

        /// <summary>
        /// قبول طلب صيانة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MaintenanceRequests.Manage")]
        public async Task<IActionResult> AcceptMaintenanceRequest(int id, string? assignedToId)
        {
            var request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            var currentUser = await _userManager.GetUserAsync(User);

            request.Status = MaintenanceRequestStatus.Accepted;
            request.AcceptedDate = DateTime.Now;
            request.ProcessedById = currentUser?.Id;
            
            if (!string.IsNullOrEmpty(assignedToId))
            {
                request.AssignedToId = assignedToId;
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم قبول طلب الصيانة";
            _logger.LogInformation($"تم قبول طلب الصيانة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
        }

        /// <summary>
        /// بدء العمل على طلب صيانة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MaintenanceRequests.Manage")]
        public async Task<IActionResult> StartMaintenanceRequest(int id)
        {
            var request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            request.Status = MaintenanceRequestStatus.InProgress;
            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تم بدء العمل على طلب الصيانة";
            return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
        }

        /// <summary>
        /// إتمام طلب صيانة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MaintenanceRequests.Manage")]
        public async Task<IActionResult> CompleteMaintenanceRequest(
            int id,
            string? faultExplanation,
            string? fixExplanation,
            string? technicianNotes,
            List<MaintenanceType>? selectedMaintenanceTypes)
        {
            var request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            var currentUser = await _userManager.GetUserAsync(User);

            var fault = string.IsNullOrWhiteSpace(faultExplanation) ? request.Description : faultExplanation.Trim();
            if (string.IsNullOrWhiteSpace(fault))
            {
                TempData["Error"] = "يرجى توضيح العطل قبل الإتمام.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            var fix = string.IsNullOrWhiteSpace(fixExplanation)
                ? "تم الإصلاح وفق العناصر المحددة."
                : fixExplanation.Trim();
            if (!string.IsNullOrWhiteSpace(technicianNotes))
            {
                fix = $"{fix}\n\nملاحظات الفني: {technicianNotes.Trim()}";
            }

            var selectedTypes = (selectedMaintenanceTypes ?? [])
                .Distinct()
                .ToList();
            if (selectedTypes.Count == 0)
            {
                TempData["Error"] = "يرجى اختيار عنصر صيانة واحد على الأقل قبل إتمام الطلب.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            request.Status = MaintenanceRequestStatus.Completed;
            request.CompletedDate = DateTime.Now;
            request.TechnicianNotes = technicianNotes;
            request.ProcessedById = currentUser?.Id;

            await _context.SaveChangesAsync();

            var invoiceResult = await _maintenanceBillingService.IssueInvoiceForCompletedRequestAsync(
                request.Id,
                currentUser?.Id ?? string.Empty,
                fault,
                fix,
                selectedTypes);
            if (!invoiceResult.Success)
            {
                TempData["Error"] = invoiceResult.ErrorMessage ?? "تم إتمام الصيانة لكن تعذر إصدار الفاتورة.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            TempData["Success"] = "✅ تم إتمام طلب الصيانة وإصدار فاتورة للمشترك.";
            _logger.LogInformation($"تم إتمام طلب الصيانة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
        }

        /// <summary>
        /// رفض طلب صيانة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MaintenanceRequests.Manage")]
        public async Task<IActionResult> RejectMaintenanceRequest(int id, string rejectionReason)
        {
            var request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] = "يجب تحديد سبب الرفض";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);

            request.Status = MaintenanceRequestStatus.Rejected;
            request.RejectionReason = rejectionReason;
            request.ProcessedById = currentUser?.Id;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم رفض طلب الصيانة";
            _logger.LogInformation($"تم رفض طلب الصيانة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(MaintenanceRequests));
        }

        /// <summary>
        /// حفظ أو تعديل موعد زيارة الصيانة (يظهر في لوحة الموظف على حسب اليوم).
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MaintenanceRequests.Manage")]
        public async Task<IActionResult> SetMaintenanceScheduledVisitDate(int id, DateTime? scheduledVisitDate)
        {
            var request = await _context.MaintenanceRequests
                .Include(m => m.Client)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (request?.Client == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            var currentUser = await _userManager.GetUserAsync(User);
            var networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
            if (!networkId.HasValue || request.Client.NetworkId != networkId.Value)
            {
                TempData["Error"] = "لا يمكن تعديل هذا الطلب في الشبكة الحالية.";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            request.ScheduledVisitDate = scheduledVisitDate;
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حفظ موعد الزيارة.";
            return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
        }

        #endregion

        private async Task<List<PricedMaintenanceOptionViewModel>> LoadPricedMaintenanceOptionsAsync(MaintenanceRequest request)
        {
            if (request.ClientId <= 0)
            {
                return [];
            }

            var clientNetworkId = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.NetworkId)
                .FirstOrDefaultAsync();
            if (!clientNetworkId.HasValue)
            {
                return [];
            }

            var net = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == clientNetworkId.Value)
                .Select(n => new { n.Id, n.ParentNetworkId })
                .FirstOrDefaultAsync();
            if (net == null)
            {
                return [];
            }

            var companyNetworkId = net.ParentNetworkId ?? net.Id;

            var rows = await _context.NetworkMaintenancePrices
                .AsNoTracking()
                .Where(p => p.NetworkId == companyNetworkId)
                .OrderByDescending(p => p.Id)
                .ToListAsync();
            if (rows.Count == 0)
            {
                return [];
            }

            var distinctByType = rows
                .GroupBy(p => p.MaintenanceType)
                .Select(g => g.First())
                .Where(p => p.IsActive && MaintenanceCatalog.IsSolutionType(p.MaintenanceType))
                .OrderBy(p => MaintenanceCatalog.GetOrder(p.MaintenanceType))
                .ToList();

            return distinctByType.Select(p => new PricedMaintenanceOptionViewModel
            {
                MaintenanceType = p.MaintenanceType,
                DisplayName = MaintenanceCatalog.GetDisplayName(p.MaintenanceType),
                AmountSYP = p.AmountSYP,
                IsDefaultForRequestType = false
            }).ToList();
        }

        private async Task<decimal> LoadMaintenanceTransportFeeAsync(MaintenanceRequest request)
        {
            if (request.ClientId <= 0)
            {
                return 0m;
            }

            var clientNetworkId = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.NetworkId)
                .FirstOrDefaultAsync();
            if (!clientNetworkId.HasValue)
            {
                return 0m;
            }

            var net = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == clientNetworkId.Value)
                .Select(n => new { n.Id, n.ParentNetworkId })
                .FirstOrDefaultAsync();
            if (net == null)
            {
                return 0m;
            }

            var companyNetworkId = net.ParentNetworkId ?? net.Id;

            var transport = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.MaintenanceTransportFee &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();

            return transport?.AmountSYP ?? 0m;
        }

        private async Task<MaintenanceCommissionPreview> LoadMaintenanceCommissionPreviewAsync(MaintenanceRequest request)
        {
            if (request.ClientId <= 0)
            {
                return new MaintenanceCommissionPreview(MaintenanceCommissionMode.Fixed, 0m);
            }

            var clientNetworkId = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.NetworkId)
                .FirstOrDefaultAsync();
            if (!clientNetworkId.HasValue)
            {
                return new MaintenanceCommissionPreview(MaintenanceCommissionMode.Fixed, 0m);
            }

            var net = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == clientNetworkId.Value)
                .Select(n => new { n.Id, n.ParentNetworkId })
                .FirstOrDefaultAsync();
            if (net == null)
            {
                return new MaintenanceCommissionPreview(MaintenanceCommissionMode.Fixed, 0m);
            }

            var pricing = await _context.FeaturePricings
                .AsNoTracking()
                .Where(p =>
                    p.IsActive &&
                    p.FeatureKey == FeatureKeys.MaintenanceCommission &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime)
                .OrderByDescending(p => p.Id)
                .FirstOrDefaultAsync();
            if (pricing == null)
            {
                return new MaintenanceCommissionPreview(MaintenanceCommissionMode.Fixed, 0m);
            }

            var mode = pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount
                ? MaintenanceCommissionMode.Percent
                : MaintenanceCommissionMode.Fixed;

            return new MaintenanceCommissionPreview(mode, pricing.AmountSYP);
        }

        private sealed record MaintenanceCommissionPreview(MaintenanceCommissionMode Mode, decimal Value);

        #region إدارة طلبات تغيير السرعة

        /// <summary>
        /// قائمة طلبات تغيير السرعة
        /// </summary>
        public async Task<IActionResult> SpeedChangeRequests(string? status = null)
        {
            var query = _context.SpeedChangeRequests
                .Include(s => s.Client)
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Include(s => s.ProcessedBy)
                .AsQueryable();

            // تصفية حسب الحالة
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<SpeedChangeRequestStatus>(status, out var statusEnum))
                {
                    query = query.Where(s => s.Status == statusEnum);
                }
            }

            var requests = await query
                .OrderByDescending(s => s.RequestDate)
                .ToListAsync();

            ViewBag.CurrentStatus = status;
            return View(requests);
        }

        /// <summary>
        /// تفاصيل طلب تغيير سرعة
        /// </summary>
        public async Task<IActionResult> SpeedChangeRequestDetails(int id)
        {
            var request = await _context.SpeedChangeRequests
                .Include(s => s.Client)
                    .ThenInclude(c => c!.MikroTikServer)
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Include(s => s.ProcessedBy)
                .Include(s => s.ImplementedBy)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            return View(request);
        }

        /// <summary>
        /// الموافقة على طلب تغيير سرعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApproveSpeedChangeRequest(int id, string? adminNotes)
        {
            if (!await CanApproveOrRejectSpeedChangeAsync())
            {
                TempData["Error"] = "ليس لديك صلاحية الموافقة على طلبات تغيير السرعة";
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            var request = await _context.SpeedChangeRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            var currentUser = await _userManager.GetUserAsync(User);

            request.Status = SpeedChangeRequestStatus.Approved;
            request.ProcessedDate = DateTime.Now;
            request.ProcessedById = currentUser?.Id;
            request.AdminNotes = adminNotes;

            await _context.SaveChangesAsync();

            TempData["Success"] = "✅ تمت الموافقة على طلب تغيير السرعة. يمكنك الآن تنفيذ التغيير.";
            _logger.LogInformation($"تمت الموافقة على طلب تغيير السرعة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
        }

        /// <summary>
        /// تنفيذ تغيير السرعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImplementSpeedChange(int id)
        {
            if (!await CanImplementSpeedChangeAsync())
            {
                TempData["Error"] = "ليس لديك صلاحية تنفيذ تغيير السرعة";
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            var request = await _context.SpeedChangeRequests
                .Include(s => s.Client)
                .Include(s => s.RequestedProfile)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            if (request.Status != SpeedChangeRequestStatus.Approved)
            {
                TempData["Error"] = "يجب الموافقة على الطلب قبل التنفيذ";
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);

            try
            {
                // تحديث البروفايل في قاعدة البيانات
                var client = request.Client;
                if (client != null)
                {
                    var oldProfileId = client.ProfileId;
                    client.ProfileId = request.RequestedProfileId;
                    client.ProfileName = request.RequestedProfile?.Name;
                    client.LastUpdated = DateTime.Now;

                    // تحديث في المايكروتك
                    if (client.MikroTikServerId.HasValue)
                    {
                        await _mikroTikService.UpdatePPPoEUser(client);
                    }

                    _context.Update(client);
                }

                // تحديث حالة الطلب
                request.Status = SpeedChangeRequestStatus.Implemented;
                request.ImplementedDate = DateTime.Now;
                request.ImplementedById = currentUser?.Id;

                await _context.SaveChangesAsync();

                TempData["Success"] = "✅ تم تنفيذ تغيير السرعة بنجاح";
                _logger.LogInformation($"تم تنفيذ تغيير السرعة للعميل {client?.Name} بواسطة {currentUser?.UserName}");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"❌ خطأ في تنفيذ التغيير: {ex.Message}";
                _logger.LogError(ex, $"خطأ في تنفيذ تغيير السرعة #{id}");
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            return RedirectToRequestsAction(nameof(SpeedChangeRequests));
        }

        /// <summary>
        /// رفض طلب تغيير سرعة
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RejectSpeedChangeRequest(int id, string rejectionReason)
        {
            if (!await CanApproveOrRejectSpeedChangeAsync())
            {
                TempData["Error"] = "ليس لديك صلاحية رفض طلبات تغيير السرعة";
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            var request = await _context.SpeedChangeRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = "الطلب غير موجود";
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] = "يجب تحديد سبب الرفض";
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            var currentUser = await _userManager.GetUserAsync(User);

            request.Status = SpeedChangeRequestStatus.Rejected;
            request.ProcessedDate = DateTime.Now;
            request.ProcessedById = currentUser?.Id;
            request.RejectionReason = rejectionReason;

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم رفض طلب تغيير السرعة";
            _logger.LogInformation($"تم رفض طلب تغيير السرعة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(SpeedChangeRequests));
        }

        #endregion
    }
}
