using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services;
using RadaTik.Services.Clients;
using RadaTik.Services.MaintenancePricing;
using RadaTik.Services.MikroTik;
using RadaTik.ViewModels.Maintenance;

namespace RadaTik.Controllers
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
        private readonly IMikroTikPppoeUserService _mikroTikService;
        private readonly IPermissionService _permissionService;
        private readonly IMaintenanceBillingService _maintenanceBillingService;
        private readonly IMaintenancePricingService _maintenancePricingService;
        private readonly IMaintenanceEmployeeTaskService _maintenanceEmployeeTasks;
        private readonly ILogger<RequestsManagementController> _logger;
        private readonly ISubscriberFaultDiagnosisService _faultDiagnosis;

        private sealed record NetworkIdParentSnapshot(int Id, int? ParentNetworkId);

        public RequestsManagementController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IMikroTikPppoeUserService mikroTikService,
            IPermissionService permissionService,
            IMaintenanceBillingService maintenanceBillingService,
            IMaintenancePricingService maintenancePricingService,
            IMaintenanceEmployeeTaskService maintenanceEmployeeTasks,
            ILogger<RequestsManagementController> logger,
            ISubscriberFaultDiagnosisService faultDiagnosis)
        {
            _context = context;
            _userManager = userManager;
            _mikroTikService = mikroTikService;
            _permissionService = permissionService;
            _maintenanceBillingService = maintenanceBillingService;
            _maintenancePricingService = maintenancePricingService;
            _maintenanceEmployeeTasks = maintenanceEmployeeTasks;
            _logger = logger;
            _faultDiagnosis = faultDiagnosis;
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
            string? currentArea = RouteData.Values["area"]?.ToString();
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
            int maintenanceCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.Pending ||
                                m.Status == MaintenanceRequestStatus.Accepted ||
                                m.Status == MaintenanceRequestStatus.InProgress);

            int speedChangeCount = await _context.SpeedChangeRequests
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
            int pendingRequestsCount = await _context.MaintenanceRequests
                .CountAsync(m => m.Status == MaintenanceRequestStatus.Pending);

            IQueryable<MaintenanceRequest> query = _context.MaintenanceRequests
                .Include(m => m.Client)
                .Include(m => m.AssignedTo)
                .AsQueryable();

            // تصفية حسب الحالة
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<MaintenanceRequestStatus>(status, out MaintenanceRequestStatus statusEnum))
                {
                    query = query.Where(m => m.Status == statusEnum);
                }
            }

            List<MaintenanceRequest> requests = await query
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
            MaintenanceRequest? request = await _context.MaintenanceRequests
                .Include(m => m.Client)
                    .ThenInclude(c => c!.Profile)
                .Include(m => m.Client)
                    .ThenInclude(c => c!.Receiver)
                .Include(m => m.AssignedTo)
                .Include(m => m.ProcessedBy)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            // جلب قائمة الموظفين للتعيين ضمن شركة المشترك
            ViewBag.AvailableStaff = new List<ApplicationUser>();
            if (request.ClientId > 0)
            {
                int? companyNetworkId = await _maintenanceEmployeeTasks.ResolveCompanyNetworkIdForClientAsync(request.ClientId);
                if (companyNetworkId.HasValue)
                {
                    ViewBag.AvailableStaff = await _maintenanceEmployeeTasks.GetAssignableEmployeesAsync(companyNetworkId.Value);
                }
            }
            ViewBag.MaintenanceInvoice = await _context.MaintenanceInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.MaintenanceRequestId == request.Id);
            ViewBag.PricedMaintenanceOptions = await LoadPricedMaintenanceOptionsAsync(request);
            ViewBag.MaintenanceTransportFee = await LoadMaintenanceTransportFeeAsync(request);
            MaintenanceCommissionPreview commissionPreview = await LoadMaintenanceCommissionPreviewAsync(request);
            ViewBag.MaintenanceCommissionMode = commissionPreview.Mode;
            ViewBag.MaintenanceCommissionValue = commissionPreview.Value;
            ViewBag.FaultDiagnosis = await _faultDiagnosis.GetForMaintenanceRequestAsync(request.Id);

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
            MaintenanceRequest? request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToAction(nameof(MaintenanceRequests));
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            request.Status = MaintenanceRequestStatus.Accepted;
            request.AcceptedDate = DateTime.Now;
            request.ProcessedById = currentUser?.Id;

            if (!string.IsNullOrWhiteSpace(assignedToId))
            {
                int? companyNetworkId = await _maintenanceEmployeeTasks.ResolveCompanyNetworkIdForClientAsync(request.ClientId);
                if (companyNetworkId.HasValue
                    && await _maintenanceEmployeeTasks.IsAssignableEmployeeAsync(companyNetworkId.Value, assignedToId))
                {
                    request.AssignedToId = assignedToId;
                }
            }

            await _context.SaveChangesAsync();
            await _maintenanceEmployeeTasks.EnsureTaskForAssignedMaintenanceAsync(request, currentUser?.Id);

            TempData["Success"] = AppMessages.OperationSuccess;
            _logger.LogInformation($"تم قبول طلب الصيانة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
        }

        /// <summary>
        /// إسناد أو تغيير موظف مهمة الصيانة بعد تقديم الطلب.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequirePermission("MaintenanceRequests.Manage")]
        public async Task<IActionResult> AssignMaintenanceEmployee(int id, string assignedToId)
        {
            MaintenanceRequest? request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            if (request.Status is MaintenanceRequestStatus.Rejected
                or MaintenanceRequestStatus.Cancelled
                or MaintenanceRequestStatus.Completed)
            {
                TempData["Error"] = "لا يمكن إسناد الطلب في هذه الحالة.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            if (string.IsNullOrWhiteSpace(assignedToId))
            {
                TempData["Error"] = "يجب اختيار موظف لإسناد المهمة.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            int? companyNetworkId = await _maintenanceEmployeeTasks.ResolveCompanyNetworkIdForClientAsync(request.ClientId);
            if (!companyNetworkId.HasValue
                || !await _maintenanceEmployeeTasks.IsAssignableEmployeeAsync(companyNetworkId.Value, assignedToId))
            {
                TempData["Error"] = "الموظف المحدد غير متاح لإسناد مهمة الصيانة.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            request.AssignedToId = assignedToId;
            await _context.SaveChangesAsync();
            await _maintenanceEmployeeTasks.EnsureTaskForAssignedMaintenanceAsync(request, currentUser?.Id);

            TempData["Success"] = AppMessages.OperationSuccess;
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
            MaintenanceRequest? request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            request.Status = MaintenanceRequestStatus.InProgress;
            await _context.SaveChangesAsync();

            TempData["Success"] = AppMessages.OperationSuccess;
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
            List<MaintenanceType>? selectedMaintenanceTypes,
            decimal? transportFeeOverride)
        {
            MaintenanceRequest? request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            string fault = string.IsNullOrWhiteSpace(faultExplanation) ? request.Description : faultExplanation.Trim();
            if (string.IsNullOrWhiteSpace(fault))
            {
                TempData["Error"] = "يرجى توضيح العطل قبل الإتمام.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            string fix = string.IsNullOrWhiteSpace(fixExplanation)
                ? "تم الإصلاح وفق العناصر المحددة."
                : fixExplanation.Trim();
            if (!string.IsNullOrWhiteSpace(technicianNotes))
            {
                fix = $"{fix}\n\nملاحظات الفني: {technicianNotes.Trim()}";
            }

            List<MaintenanceType> selectedTypes = (selectedMaintenanceTypes ?? [])
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

            try
            {
                await _faultDiagnosis.ConfirmFromMaintenanceAsync(id, selectedTypes, currentUser?.Id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذر حفظ تأكيد سبب العطل لطلب الصيانة {RequestId}", id);
            }

            MaintenanceInvoiceIssueResult invoiceResult = await _maintenanceBillingService.IssueInvoiceForCompletedRequestAsync(
                request.Id,
                currentUser?.Id ?? string.Empty,
                fault,
                fix,
                selectedTypes,
                transportFeeOverride);
            if (!invoiceResult.Success)
            {
                TempData["Error"] = invoiceResult.ErrorMessage ?? "تم إتمام الصيانة لكن تعذر إصدار الفاتورة.";
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            TempData["Success"] = AppMessages.OperationSuccess;
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
            MaintenanceRequest? request = await _context.MaintenanceRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] = AppMessages.MustSpecifyRejectionReason;
                return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            request.Status = MaintenanceRequestStatus.Rejected;
            request.RejectionReason = rejectionReason;
            request.ProcessedById = currentUser?.Id;

            await _context.SaveChangesAsync();
            await _maintenanceEmployeeTasks.CancelLinkedOpenTaskAsync(id);

            TempData["Success"] = AppMessages.OperationSuccess;
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
            MaintenanceRequest? request = await _context.MaintenanceRequests
                .Include(m => m.Client)
                .FirstOrDefaultAsync(m => m.Id == id);
            if (request?.Client == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);
            int? networkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, currentUser);
            if (!networkId.HasValue || request.Client.NetworkId != networkId.Value)
            {
                TempData["Error"] = "لا يمكن تعديل هذا الطلب في الشبكة الحالية.";
                return RedirectToRequestsAction(nameof(MaintenanceRequests));
            }

            request.ScheduledVisitDate = scheduledVisitDate;
            await _context.SaveChangesAsync();

            TempData["Success"] = AppMessages.OperationSuccess;
            return RedirectToRequestsAction(nameof(MaintenanceRequestDetails), new { id });
        }

        #endregion

        private async Task<List<PricedMaintenanceOptionViewModel>> LoadPricedMaintenanceOptionsAsync(MaintenanceRequest request)
        {
            if (request.ClientId <= 0)
            {
                return [];
            }

            int? clientNetworkId = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.NetworkId)
                .FirstOrDefaultAsync();
            if (!clientNetworkId.HasValue)
            {
                return [];
            }
            return await _maintenancePricingService.LoadPricedSolutionOptionsAsync(clientNetworkId.Value);
        }

        private async Task<decimal> LoadMaintenanceTransportFeeAsync(MaintenanceRequest request)
        {
            if (request.ClientId <= 0)
            {
                return 0m;
            }

            int? clientNetworkId = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.NetworkId)
                .FirstOrDefaultAsync();
            if (!clientNetworkId.HasValue)
            {
                return 0m;
            }

            NetworkIdParentSnapshot? net = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == clientNetworkId.Value)
                .Select(n => new NetworkIdParentSnapshot(n.Id, n.ParentNetworkId))
                .FirstOrDefaultAsync();
            if (net == null)
            {
                return 0m;
            }

            int companyNetworkId = net.ParentNetworkId ?? net.Id;

            FeaturePricing? transport = await _context.FeaturePricings
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

            int? clientNetworkId = await _context.Clients
                .AsNoTracking()
                .Where(c => c.Id == request.ClientId)
                .Select(c => c.NetworkId)
                .FirstOrDefaultAsync();
            if (!clientNetworkId.HasValue)
            {
                return new MaintenanceCommissionPreview(MaintenanceCommissionMode.Fixed, 0m);
            }

            NetworkIdParentSnapshot? net = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == clientNetworkId.Value)
                .Select(n => new NetworkIdParentSnapshot(n.Id, n.ParentNetworkId))
                .FirstOrDefaultAsync();
            if (net == null)
            {
                return new MaintenanceCommissionPreview(MaintenanceCommissionMode.Fixed, 0m);
            }

            FeaturePricing? pricing = await _context.FeaturePricings
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

            MaintenanceCommissionMode mode = pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount
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
            IQueryable<SpeedChangeRequest> query = _context.SpeedChangeRequests
                .Include(s => s.Client)
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Include(s => s.ProcessedBy)
                .AsQueryable();

            // تصفية حسب الحالة
            if (!string.IsNullOrEmpty(status))
            {
                if (Enum.TryParse<SpeedChangeRequestStatus>(status, out SpeedChangeRequestStatus statusEnum))
                {
                    query = query.Where(s => s.Status == statusEnum);
                }
            }

            List<SpeedChangeRequest> requests = await query
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
            SpeedChangeRequest? request = await _context.SpeedChangeRequests
                .Include(s => s.Client)
                    .ThenInclude(c => c!.MikroTikServer)
                .Include(s => s.CurrentProfile)
                .Include(s => s.RequestedProfile)
                .Include(s => s.ProcessedBy)
                .Include(s => s.ImplementedBy)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
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

            SpeedChangeRequest? request = await _context.SpeedChangeRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            request.Status = SpeedChangeRequestStatus.Approved;
            request.ProcessedDate = DateTime.Now;
            request.ProcessedById = currentUser?.Id;
            request.AdminNotes = adminNotes;

            await _context.SaveChangesAsync();

            TempData["Success"] = AppMessages.OperationSuccess;
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

            SpeedChangeRequest? request = await _context.SpeedChangeRequests
                .Include(s => s.Client)
                .Include(s => s.RequestedProfile)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            if (request.Status != SpeedChangeRequestStatus.Approved)
            {
                TempData["Error"] = "يجب الموافقة على الطلب قبل التنفيذ";
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            try
            {
                // تحديث البروفايل في قاعدة البيانات
                Client? client = request.Client;
                if (client != null)
                {
                    int oldProfileId = client.ProfileId;
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

                TempData["Success"] = AppMessages.OperationSuccess;
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

            SpeedChangeRequest? request = await _context.SpeedChangeRequests.FindAsync(id);
            if (request == null)
            {
                TempData["Error"] = AppMessages.RequestNotFound;
                return RedirectToRequestsAction(nameof(SpeedChangeRequests));
            }

            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                TempData["Error"] = AppMessages.MustSpecifyRejectionReason;
                return RedirectToRequestsAction(nameof(SpeedChangeRequestDetails), new { id });
            }

            ApplicationUser? currentUser = await _userManager.GetUserAsync(User);

            request.Status = SpeedChangeRequestStatus.Rejected;
            request.ProcessedDate = DateTime.Now;
            request.ProcessedById = currentUser?.Id;
            request.RejectionReason = rejectionReason;

            await _context.SaveChangesAsync();

            TempData["Success"] = AppMessages.OperationSuccess;
            _logger.LogInformation($"تم رفض طلب تغيير السرعة #{id} بواسطة {currentUser?.UserName}");

            return RedirectToRequestsAction(nameof(SpeedChangeRequests));
        }

        #endregion
    }
}
