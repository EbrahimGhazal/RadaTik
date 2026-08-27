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
using global::RadaTik.Services.Clients;
using global::RadaTik.Services.MikroTik;
using global::RadaTik.Services.PricingPolicies;
using global::RadaTik.Models.Business;

namespace RadaTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkOrSystemAdministrator)]
public class EmployeeServiceApprovalsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMikroTikPppoeUserService _mikroTikService;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
    private readonly ISenderPricingOrchestrator _senderPricingOrchestrator;
    private readonly ISubscriberInstallationInvoiceService _subscriberInstallationInvoiceService;
    private readonly ILogger<EmployeeServiceApprovalsController> _logger;

    public EmployeeServiceApprovalsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikPppoeUserService mikroTikService,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ISenderPricingOrchestrator senderPricingOrchestrator,
        ISubscriberInstallationInvoiceService subscriberInstallationInvoiceService,
        ILogger<EmployeeServiceApprovalsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _mikroTikService = mikroTikService;
        _usageChargeService = usageChargeService;
        _senderPricingOrchestrator = senderPricingOrchestrator;
        _subscriberInstallationInvoiceService = subscriberInstallationInvoiceService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? focusRequestId = null)
    {
        ApplicationUser? manager = await _userManager.GetUserAsync(User);
        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, manager);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToRoute("networkManager-network");
        }

        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        List<NetworkServiceRequest> items = await _context.NetworkServiceRequests
            .AsNoTracking()
            .Include(r => r.Network)
            .Include(r => r.RequestedByUser)
            .Where(r =>
                companyScope.Contains(r.NetworkId) &&
                r.Status == NetworkServiceRequestStatus.Pending &&
                (
                    (r.Notes != null && r.Notes.StartsWith("EMP_REQ:")) ||
                    (r.Notes != null && r.Notes.Contains("SECTOR_CREATE_PENDING:"))
                ))
            .OrderByDescending(r => r.RequestedAt)
            .Take(300)
            .ToListAsync();

        ViewBag.FocusRequestId = focusRequestId;
        return View(items);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes = null)
    {
        ApplicationUser? manager = await _userManager.GetUserAsync(User);
        if (manager == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, manager);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        EmployeeApprovalRequestKind? approvedClientKind = null;
        int? approvedClientId = null;

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
        try
        {
            NetworkServiceRequest? request = await _context.NetworkServiceRequests
                .FirstOrDefaultAsync(r => r.Id == id && companyScope.Contains(r.NetworkId));
            if (request == null)
            {
                return NotFound();
            }

            if (request.Status != NetworkServiceRequestStatus.Pending)
            {
                TempData["Error"] = "لا يمكن اعتماد طلب غير معلّق.";
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(request.FeatureKey, FeatureKeys.Sectors, StringComparison.OrdinalIgnoreCase))
            {
                SenderApprovalOutcome senderApproval = await _senderPricingOrchestrator.TryHandlePendingApprovalAsync(request, manager.Id, notes);
                if (!senderApproval.Handled)
                {
                    TempData["Error"] = "تعذر معالجة طلب المرسل الحالي.";
                    return RedirectToAction(nameof(Index));
                }

                if (senderApproval.OutcomeType != SenderApprovalOutcomeType.ApprovedAndCharged)
                {
                    TempData["Error"] = senderApproval.Message;
                    return RedirectToAction(nameof(Index));
                }

                await tx.CommitAsync();
                TempData["Success"] = senderApproval.Message;
                return RedirectToAction(nameof(Index));
            }

            if (!EmployeeApprovalRequestHelper.TryParse(request.Notes, out EmployeeApprovalRequestKind kind, out int entityId, out string? payloadJson))
            {
                TempData["Error"] = "تنسيق الطلب غير معروف.";
                return RedirectToAction(nameof(Index));
            }

            switch (kind)
            {
                case EmployeeApprovalRequestKind.ReceiverCreate:
                    await ApproveReceiverCreateAsync(entityId, companyScope, companyNetworkId, manager.Id);
                    break;
                case EmployeeApprovalRequestKind.ReceiverEdit:
                    await ApproveReceiverEditAsync(entityId, companyScope, payloadJson);
                    break;
                case EmployeeApprovalRequestKind.ClientCreate:
                    await ApproveClientCreateAsync(entityId, companyScope, payloadJson, manager.Id);
                    approvedClientKind = EmployeeApprovalRequestKind.ClientCreate;
                    approvedClientId = entityId;
                    break;
                case EmployeeApprovalRequestKind.ClientEdit:
                    await ApproveClientEditAsync(entityId, companyScope, payloadJson, manager.Id);
                    approvedClientKind = EmployeeApprovalRequestKind.ClientEdit;
                    approvedClientId = entityId;
                    break;
                default:
                    throw new InvalidOperationException("نوع الطلب غير مدعوم.");
            }

            request.Status = NetworkServiceRequestStatus.Approved;
            request.DecidedByUserId = manager.Id;
            request.DecidedAt = DateTime.Now;
            request.Notes = string.IsNullOrWhiteSpace(notes)
                ? request.Notes
                : $"{request.Notes}\nManager note: {notes.Trim()}";

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Success"] = AppMessages.OperationSuccess;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogWarning(ex, "Failed to approve employee service request #{RequestId}.", id);
            TempData["Error"] = $"تعذر اعتماد الطلب: {ex.Message}";
        }

        if (approvedClientKind == EmployeeApprovalRequestKind.ClientCreate && approvedClientId.HasValue)
        {
            TempData["ResumeWizardClientId"] = approvedClientId.Value.ToString();
            if (TempData["Info"] == null)
            {
                TempData["Info"] =
                    "تم اعتماد المشترك. أكمل إصدار فاتورة التركيب من ملف العميل أو معالج المشترك.";
            }

            return RedirectToAction("Details", "Clients", new { area = "CompanyAdmin", id = approvedClientId.Value });
        }

        if (approvedClientKind == EmployeeApprovalRequestKind.ClientEdit && approvedClientId.HasValue)
        {
            return RedirectToAction("Details", "Clients", new { area = "CompanyAdmin", id = approvedClientId.Value });
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? notes = null)
    {
        ApplicationUser? manager = await _userManager.GetUserAsync(User);
        if (manager == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        int? selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, manager);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = AppMessages.SelectNetworkFirst;
            return RedirectToAction(nameof(Index));
        }

        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        await using IDbContextTransaction tx = await _context.Database.BeginTransactionAsync();
        try
        {
            NetworkServiceRequest? request = await _context.NetworkServiceRequests
                .FirstOrDefaultAsync(r => r.Id == id && companyScope.Contains(r.NetworkId));
            if (request == null)
            {
                return NotFound();
            }

            if (request.Status != NetworkServiceRequestStatus.Pending)
            {
                TempData["Error"] = "لا يمكن رفض طلب غير معلّق.";
                return RedirectToAction(nameof(Index));
            }

            if (string.Equals(request.FeatureKey, FeatureKeys.Sectors, StringComparison.OrdinalIgnoreCase))
            {
                await _senderPricingOrchestrator.TryHandlePendingRejectionAsync(request);
                request.Status = NetworkServiceRequestStatus.Rejected;
                request.DecidedByUserId = manager.Id;
                request.DecidedAt = DateTime.Now;
                request.Notes = string.IsNullOrWhiteSpace(notes)
                    ? request.Notes
                    : $"{request.Notes}\nReject reason: {notes.Trim()}";

                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                TempData["Success"] = AppMessages.OperationSuccess;
                return RedirectToAction(nameof(Index));
            }

            if (EmployeeApprovalRequestHelper.TryParse(request.Notes, out EmployeeApprovalRequestKind kind, out int entityId, out _))
            {
                if (kind == EmployeeApprovalRequestKind.ReceiverCreate)
                {
                    Receiver? receiver = await _context.Receivers
                        .FirstOrDefaultAsync(r =>
                            r.Id == entityId &&
                            r.NetworkId.HasValue &&
                            companyScope.Contains(r.NetworkId.Value));
                    if (receiver != null)
                    {
                        _context.Receivers.Remove(receiver);
                    }
                }
                else if (kind == EmployeeApprovalRequestKind.ClientCreate)
                {
                    Client? client = await _context.Clients
                        .FirstOrDefaultAsync(c =>
                            c.Id == entityId &&
                            c.NetworkId.HasValue &&
                            companyScope.Contains(c.NetworkId.Value));
                    if (client != null)
                    {
                        List<SubscriberInstallationInvoice> draftInvoices = await _context.SubscriberInstallationInvoices
                            .Where(i => i.ClientId == client.Id && i.Status == SubscriberInstallationInvoiceStatus.Draft)
                            .ToListAsync();
                        _context.SubscriberInstallationInvoices.RemoveRange(draftInvoices);
                        _context.Clients.Remove(client);
                    }
                }
            }

            request.Status = NetworkServiceRequestStatus.Rejected;
            request.DecidedByUserId = manager.Id;
            request.DecidedAt = DateTime.Now;
            request.Notes = string.IsNullOrWhiteSpace(notes)
                ? request.Notes
                : $"{request.Notes}\nReject reason: {notes.Trim()}";

            await _context.SaveChangesAsync();
            await tx.CommitAsync();
            TempData["Success"] = AppMessages.OperationSuccess;
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogWarning(ex, "Failed to reject employee service request #{RequestId}.", id);
            TempData["Error"] = "تعذر رفض الطلب.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task ApproveReceiverCreateAsync(
        int receiverId,
        IReadOnlyCollection<int> companyScope,
        int companyNetworkId,
        string actorUserId)
    {
        Receiver? receiver = await _context.Receivers
            .FirstOrDefaultAsync(r =>
                r.Id == receiverId &&
                r.NetworkId.HasValue &&
                companyScope.Contains(r.NetworkId.Value));
        if (receiver == null)
        {
            throw new InvalidOperationException("تعذر العثور على المستقبل المطلوب.");
        }

        receiver.IsActive = true;

        // خصم سعر إضافة "لاقط/مستقبل" عند اعتماد مدير الشركة للإنشاء.
        await _usageChargeService.ChargeUsageIncreaseAsync(
            companyNetworkId,
            actorUserId,
            PricingChargeUnit.PerReceiver);
    }

    private async Task ApproveReceiverEditAsync(int receiverId, IReadOnlyCollection<int> companyScope, string? payloadJson)
    {
        ReceiverEditApprovalPayload payload = EmployeeApprovalRequestHelper.DeserializePayload<ReceiverEditApprovalPayload>(payloadJson)
            ?? throw new InvalidOperationException("بيانات تعديل المستقبل غير صالحة.");

        Receiver? receiver = await _context.Receivers
            .FirstOrDefaultAsync(r =>
                r.Id == receiverId &&
                r.NetworkId.HasValue &&
                companyScope.Contains(r.NetworkId.Value));
        if (receiver == null)
        {
            throw new InvalidOperationException("تعذر العثور على المستقبل المطلوب.");
        }

        bool sectorValid = await _context.Sectors.AnyAsync(s =>
            s.Id == payload.SectorId &&
            s.NetworkId.HasValue &&
            companyScope.Contains(s.NetworkId.Value) &&
            s.IsActive);
        if (!sectorValid)
        {
            throw new InvalidOperationException("المرسل المحدد للتعديل غير متاح.");
        }

        receiver.Name = payload.Name;
        receiver.SectorId = payload.SectorId;
        receiver.IPAddress = payload.IPAddress;
        receiver.NetworkMask = payload.NetworkMask;
        receiver.Latitude = payload.Latitude;
        receiver.Longitude = payload.Longitude;
        receiver.ElevationMeters = payload.ElevationMeters;
        receiver.AntennaHeightAglMeters = payload.AntennaHeightAglMeters;
        receiver.IsActive = payload.IsActive;
    }

    private async Task ApproveClientCreateAsync(int clientId, IReadOnlyCollection<int> companyScope, string? payloadJson, string managerUserId)
    {
        ClientApprovalPayload? payload = EmployeeApprovalRequestHelper.DeserializePayload<ClientApprovalPayload>(payloadJson);

        Client? client = await _context.Clients
            .FirstOrDefaultAsync(c =>
                c.Id == clientId &&
                c.NetworkId.HasValue &&
                companyScope.Contains(c.NetworkId.Value));
        if (client == null)
        {
            throw new InvalidOperationException("تعذر العثور على المشترك المطلوب.");
        }

        if (!client.MikroTikServerId.HasValue)
        {
            throw new InvalidOperationException("لا يمكن اعتماد المشترك دون خادم MikroTik.");
        }

        string? dbUserName = string.IsNullOrWhiteSpace(payload?.DbUserName) ? client.UserName : payload.DbUserName.Trim();
        string? dbPassword = string.IsNullOrWhiteSpace(payload?.DbPassword) ? client.Password : payload.DbPassword.Trim();
        if (string.IsNullOrWhiteSpace(dbUserName) || string.IsNullOrWhiteSpace(dbPassword))
        {
            throw new InvalidOperationException("بيانات حساب النظام غير مكتملة لإنشاء المشترك.");
        }

        ApplicationUser? existingIdentityUser = await _userManager.FindByNameAsync(dbUserName);
        if (existingIdentityUser != null && existingIdentityUser.ClientId != client.Id)
        {
            throw new InvalidOperationException("اسم مستخدم حساب النظام مستخدم مسبقاً.");
        }

        client.IsActive = true;
        client.ConnectionStatus = "مفعل";
        client.LastUpdated = DateTime.Now;

        try
        {
            await _mikroTikService.AddPPPoEUser(client);
        }
        catch (Exception ex) when (MikroTikApiSupport.IsAlreadyExistsMessage(ex))
        {
            _logger.LogInformation(
                "PPPoE user {UserName} already exists on MikroTik during employee client-create approval for client {ClientId}.",
                client.UserName,
                client.Id);
        }

        if (existingIdentityUser == null)
        {
            string userEmail = dbUserName.Contains("@", StringComparison.Ordinal)
                ? dbUserName
                : $"{dbUserName}@radatik.local";
            ApplicationUser newUser = new ApplicationUser
            {
                UserName = dbUserName,
                Email = userEmail,
                FullName = client.Name,
                PhoneNumber = client.PhoneNumber,
                CreatedDate = DateTime.Now,
                IsActive = true,
                ClientId = client.Id,
                NetworkId = client.NetworkId,
                MustChangePassword = true
            };

            IdentityResult createResult = await _userManager.CreateAsync(newUser, dbPassword);
            if (!createResult.Succeeded)
            {
                string errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"تعذر إنشاء حساب النظام: {errors}");
            }

            await _userManager.AddToRoleAsync(newUser, RoleNames.Client);
        }

        if (!client.NetworkId.HasValue)
        {
            throw new InvalidOperationException("المشترك غير مرتبط بشبكة.");
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(client.NetworkId.Value);
        await _usageChargeService.ChargeUsageIncreaseAsync(
            companyNetworkId,
            managerUserId,
            PricingChargeUnit.PerSubscriber);

        await _subscriberInstallationInvoiceService.CreateInitialSetupInvoiceAsync(client, managerUserId);

        SubscriberInstallationInvoice? draftInvoice = await _context.SubscriberInstallationInvoices
            .AsNoTracking()
            .FirstOrDefaultAsync(i =>
                i.ClientId == client.Id && i.Kind == SubscriberInstallationInvoiceKind.InitialSetup);
        if (draftInvoice != null)
        {
            TempData["Info"] =
                "تم اعتماد المشترك وإنشاء الحساب على سيرفر MikroTik. أكمل إصدار فاتورة التركيب من ملف المشترك.";
        }
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId)
    {
        int? parentId = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId)
            .Select(n => n.ParentNetworkId)
            .FirstOrDefaultAsync();
        return parentId ?? networkId;
    }

    private async Task ApproveClientEditAsync(int clientId, IReadOnlyCollection<int> companyScope, string? payloadJson, string managerUserId)
    {
        ClientApprovalPayload payload = EmployeeApprovalRequestHelper.DeserializePayload<ClientApprovalPayload>(payloadJson)
            ?? throw new InvalidOperationException("بيانات تعديل العميل غير صالحة.");

        Client? client = await _context.Clients
            .AsTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == clientId &&
                c.NetworkId.HasValue &&
                companyScope.Contains(c.NetworkId.Value));
        if (client == null)
        {
            throw new InvalidOperationException("تعذر العثور على العميل المطلوب.");
        }

        string? originalUserName = client.UserName;
        int? previousReceiverId = client.ReceiverId;

        client.Name = payload.Name ?? client.Name;
        client.UserName = payload.UserName ?? client.UserName;
        client.PhoneNumber = payload.PhoneNumber ?? client.PhoneNumber;
        client.ResidenceAddress = payload.ResidenceAddress;
        client.Occupation = payload.Occupation;
        client.Workplace = payload.Workplace;
        client.Latitude = payload.Latitude;
        client.Longitude = payload.Longitude;
        client.PowerSource = payload.PowerSource;
        client.Building = payload.Building;
        client.Floor = payload.Floor;
        client.ReceiverId = payload.ReceiverId;
        ClientVipAssignment.Apply(client, payload.IsVip, payload.VipNote, DateTime.Now);
        if (!string.IsNullOrWhiteSpace(payload.Password))
        {
            client.Password = payload.Password;
        }

        client.LastUpdated = DateTime.Now;
        await _subscriberInstallationInvoiceService.CreateReceiverUpgradeInvoiceIfNeededAsync(client, previousReceiverId, managerUserId);

        if (client.MikroTikServerId.HasValue)
        {
            if (!string.Equals(originalUserName, client.UserName, StringComparison.Ordinal))
            {
                await _mikroTikService.UpdatePPPoEUserWithOriginalUsername(client, originalUserName ?? string.Empty);
            }
            else
            {
                await _mikroTikService.UpdatePPPoEUser(client);
            }
        }

        ApplicationUser? linkedUser = await _context.Users.FirstOrDefaultAsync(u => u.ClientId == client.Id);
        if (linkedUser != null)
        {
            string? dbUserName = string.IsNullOrWhiteSpace(payload.DbUserName) ? linkedUser.UserName : payload.DbUserName.Trim();
            if (!string.IsNullOrWhiteSpace(dbUserName) && !string.Equals(linkedUser.UserName, dbUserName, StringComparison.Ordinal))
            {
                IdentityResult setUserNameResult = await _userManager.SetUserNameAsync(linkedUser, dbUserName);
                if (!setUserNameResult.Succeeded)
                {
                    string errors = string.Join(", ", setUserNameResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"تعذر تحديث اسم حساب النظام: {errors}");
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.DbPassword))
            {
                string token = await _userManager.GeneratePasswordResetTokenAsync(linkedUser);
                IdentityResult resetResult = await _userManager.ResetPasswordAsync(linkedUser, token, payload.DbPassword.Trim());
                if (!resetResult.Succeeded)
                {
                    string errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"تعذر تحديث كلمة مرور حساب النظام: {errors}");
                }
            }

            linkedUser.FullName = client.Name;
            linkedUser.PhoneNumber = client.PhoneNumber;
            await _userManager.UpdateAsync(linkedUser);
        }
    }
}
