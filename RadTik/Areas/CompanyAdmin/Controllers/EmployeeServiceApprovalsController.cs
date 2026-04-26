using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services;
using RadTik.Services.PricingPolicies;

namespace RadTik.Areas.CompanyAdmin.Controllers;

[Area("CompanyAdmin")]
[Authorize(Roles = RoleNames.NetworkAdministrator)]
public class EmployeeServiceApprovalsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMikroTikUsersService _mikroTikService;
    private readonly IUsageBasedSubscriptionChargeService _usageChargeService;
    private readonly ISenderPricingOrchestrator _senderPricingOrchestrator;
    private readonly ILogger<EmployeeServiceApprovalsController> _logger;

    public EmployeeServiceApprovalsController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        IMikroTikUsersService mikroTikService,
        IUsageBasedSubscriptionChargeService usageChargeService,
        ISenderPricingOrchestrator senderPricingOrchestrator,
        ILogger<EmployeeServiceApprovalsController> logger)
    {
        _context = context;
        _userManager = userManager;
        _mikroTikService = mikroTikService;
        _usageChargeService = usageChargeService;
        _senderPricingOrchestrator = senderPricingOrchestrator;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? focusRequestId = null)
    {
        var manager = await _userManager.GetUserAsync(User);
        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, manager);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToRoute("networkManager-network");
        }

        var selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
        var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        var items = await _context.NetworkServiceRequests
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
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, manager);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction(nameof(Index));
        }

        var selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
        var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var request = await _context.NetworkServiceRequests
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
                var senderApproval = await _senderPricingOrchestrator.TryHandlePendingApprovalAsync(request, manager.Id, notes);
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

            if (!EmployeeApprovalRequestHelper.TryParse(request.Notes, out var kind, out var entityId, out var payloadJson))
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
                    await ApproveClientCreateAsync(entityId, companyScope, payloadJson);
                    break;
                case EmployeeApprovalRequestKind.ClientEdit:
                    await ApproveClientEditAsync(entityId, companyScope, payloadJson);
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
            TempData["Success"] = "تم اعتماد طلب الموظف وتنفيذه بنجاح.";
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogWarning(ex, "Failed to approve employee service request #{RequestId}.", id);
            TempData["Error"] = $"تعذر اعتماد الطلب: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? notes = null)
    {
        var manager = await _userManager.GetUserAsync(User);
        if (manager == null)
        {
            return RedirectToAction("Login", "Account", new { area = "" });
        }

        var selectedNetworkId = NetworkHelper.GetCurrentNetworkId(HttpContext, _context, manager);
        if (!selectedNetworkId.HasValue)
        {
            TempData["Error"] = "يرجى تحديد شبكة أولاً.";
            return RedirectToAction(nameof(Index));
        }

        var selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId.Value);
        var companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId.Value;
        var companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            var request = await _context.NetworkServiceRequests
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
                TempData["Success"] = "تم رفض طلب إضافة المرسل.";
                return RedirectToAction(nameof(Index));
            }

            if (EmployeeApprovalRequestHelper.TryParse(request.Notes, out var kind, out var entityId, out _))
            {
                if (kind == EmployeeApprovalRequestKind.ReceiverCreate)
                {
                    var receiver = await _context.Receivers
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
                    var client = await _context.Clients
                        .FirstOrDefaultAsync(c =>
                            c.Id == entityId &&
                            c.NetworkId.HasValue &&
                            companyScope.Contains(c.NetworkId.Value));
                    if (client != null)
                    {
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
            TempData["Success"] = "تم رفض الطلب.";
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
        var receiver = await _context.Receivers
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
        var payload = EmployeeApprovalRequestHelper.DeserializePayload<ReceiverEditApprovalPayload>(payloadJson)
            ?? throw new InvalidOperationException("بيانات تعديل المستقبل غير صالحة.");

        var receiver = await _context.Receivers
            .FirstOrDefaultAsync(r =>
                r.Id == receiverId &&
                r.NetworkId.HasValue &&
                companyScope.Contains(r.NetworkId.Value));
        if (receiver == null)
        {
            throw new InvalidOperationException("تعذر العثور على المستقبل المطلوب.");
        }

        var sectorValid = await _context.Sectors.AnyAsync(s =>
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

    private async Task ApproveClientCreateAsync(int clientId, IReadOnlyCollection<int> companyScope, string? payloadJson)
    {
        var payload = EmployeeApprovalRequestHelper.DeserializePayload<ClientApprovalPayload>(payloadJson)
            ?? throw new InvalidOperationException("بيانات إنشاء العميل غير صالحة.");

        var client = await _context.Clients
            .FirstOrDefaultAsync(c =>
                c.Id == clientId &&
                c.NetworkId.HasValue &&
                companyScope.Contains(c.NetworkId.Value));
        if (client == null)
        {
            throw new InvalidOperationException("تعذر العثور على العميل المطلوب.");
        }

        if (client.MikroTikServerId.HasValue)
        {
            await _mikroTikService.AddPPPoEUser(client);
        }

        var dbUserName = string.IsNullOrWhiteSpace(payload.DbUserName) ? client.UserName : payload.DbUserName.Trim();
        var dbPassword = string.IsNullOrWhiteSpace(payload.DbPassword) ? client.Password : payload.DbPassword.Trim();
        if (string.IsNullOrWhiteSpace(dbUserName) || string.IsNullOrWhiteSpace(dbPassword))
        {
            throw new InvalidOperationException("بيانات حساب النظام غير مكتملة لإنشاء العميل.");
        }

        var existingIdentityUser = await _userManager.FindByNameAsync(dbUserName);
        if (existingIdentityUser != null && existingIdentityUser.ClientId != client.Id)
        {
            throw new InvalidOperationException("اسم مستخدم حساب النظام مستخدم مسبقاً.");
        }

        if (existingIdentityUser == null)
        {
            var userEmail = dbUserName.Contains("@", StringComparison.Ordinal)
                ? dbUserName
                : $"{dbUserName}@radtik.local";
            var newUser = new ApplicationUser
            {
                UserName = dbUserName,
                Email = userEmail,
                FullName = client.Name,
                PhoneNumber = client.PhoneNumber,
                CreatedDate = DateTime.Now,
                IsActive = true,
                ClientId = client.Id,
                NetworkId = client.NetworkId
            };

            var createResult = await _userManager.CreateAsync(newUser, dbPassword);
            if (!createResult.Succeeded)
            {
                var errors = string.Join(", ", createResult.Errors.Select(e => e.Description));
                throw new InvalidOperationException($"تعذر إنشاء حساب النظام: {errors}");
            }

            await _userManager.AddToRoleAsync(newUser, RoleNames.Client);
        }

        client.IsActive = true;
        client.ConnectionStatus = "مفعل";
        client.LastUpdated = DateTime.Now;
    }

    private async Task ApproveClientEditAsync(int clientId, IReadOnlyCollection<int> companyScope, string? payloadJson)
    {
        var payload = EmployeeApprovalRequestHelper.DeserializePayload<ClientApprovalPayload>(payloadJson)
            ?? throw new InvalidOperationException("بيانات تعديل العميل غير صالحة.");

        var client = await _context.Clients
            .AsTracking()
            .FirstOrDefaultAsync(c =>
                c.Id == clientId &&
                c.NetworkId.HasValue &&
                companyScope.Contains(c.NetworkId.Value));
        if (client == null)
        {
            throw new InvalidOperationException("تعذر العثور على العميل المطلوب.");
        }

        var originalUserName = client.UserName;

        client.Name = payload.Name ?? client.Name;
        client.UserName = payload.UserName ?? client.UserName;
        client.PhoneNumber = payload.PhoneNumber ?? client.PhoneNumber;
        client.ResidenceAddress = payload.ResidenceAddress;
        client.Latitude = payload.Latitude;
        client.Longitude = payload.Longitude;
        client.PowerSource = payload.PowerSource;
        client.Building = payload.Building;
        client.Floor = payload.Floor;
        client.ReceiverId = payload.ReceiverId;
        if (!string.IsNullOrWhiteSpace(payload.Password))
        {
            client.Password = payload.Password;
        }

        client.LastUpdated = DateTime.Now;

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

        var linkedUser = await _context.Users.FirstOrDefaultAsync(u => u.ClientId == client.Id);
        if (linkedUser != null)
        {
            var dbUserName = string.IsNullOrWhiteSpace(payload.DbUserName) ? linkedUser.UserName : payload.DbUserName.Trim();
            if (!string.IsNullOrWhiteSpace(dbUserName) && !string.Equals(linkedUser.UserName, dbUserName, StringComparison.Ordinal))
            {
                var setUserNameResult = await _userManager.SetUserNameAsync(linkedUser, dbUserName);
                if (!setUserNameResult.Succeeded)
                {
                    var errors = string.Join(", ", setUserNameResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"تعذر تحديث اسم حساب النظام: {errors}");
                }
            }

            if (!string.IsNullOrWhiteSpace(payload.DbPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(linkedUser);
                var resetResult = await _userManager.ResetPasswordAsync(linkedUser, token, payload.DbPassword.Trim());
                if (!resetResult.Succeeded)
                {
                    var errors = string.Join(", ", resetResult.Errors.Select(e => e.Description));
                    throw new InvalidOperationException($"تعذر تحديث كلمة مرور حساب النظام: {errors}");
                }
            }

            linkedUser.FullName = client.Name;
            linkedUser.PhoneNumber = client.PhoneNumber;
            await _userManager.UpdateAsync(linkedUser);
        }
    }
}
