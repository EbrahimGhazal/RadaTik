using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Services;

/// <summary>
/// Centralized in-app notifications for newly submitted requests.
/// </summary>
public class RequestNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<RequestNotificationService> _logger;

    public RequestNotificationService(
        ApplicationDbContext context,
        ILogger<RequestNotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task NotifyMaintenanceRequestSubmittedAsync(
        MaintenanceRequest request,
        int? networkId,
        string? clientName,
        string? clientUserName)
    {
        List<string> recipients = await GetCompanyAdminRecipientsAsync(networkId);
        if (recipients.Count == 0)
        {
            return;
        }

        string title = "طلب صيانة جديد";
        string message = $"تم تقديم طلب صيانة جديد من المشترك {(clientName ?? clientUserName ?? $"#{request.ClientId}")}.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.MaintenanceRequestSubmitted,
            title,
            message,
            $"MaintenanceRequestSubmitted:{request.Id}",
            networkId);
    }

    public async Task NotifySpeedChangeRequestSubmittedAsync(
        SpeedChangeRequest request,
        string? clientName,
        string? fromProfileName,
        string? toProfileName,
        int? networkId)
    {
        List<string> recipients = await GetCompanyAdminRecipientsAsync(networkId);
        if (recipients.Count == 0)
        {
            return;
        }

        string title = "طلب تغيير سرعة جديد";
        string message = $"تم تقديم طلب تغيير سرعة من {(clientName ?? $"العميل #{request.ClientId}")} من باقة {(fromProfileName ?? "-")} إلى {(toProfileName ?? "-")}.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.SpeedChangeRequestSubmitted,
            title,
            message,
            $"SpeedChangeRequestSubmitted:{request.Id}",
            networkId);
    }

    public async Task NotifyClientJoinRequestSubmittedAsync(JoinRequest request)
    {
        List<string> recipients = await GetSystemAdminRecipientsAsync();
        if (recipients.Count == 0)
        {
            return;
        }

        string title = "طلب انضمام عميل جديد";
        string message = $"تم تقديم طلب انضمام جديد كعميل من {request.FullName} ({request.Email}).";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.ClientJoinRequestSubmitted,
            title,
            message,
            $"ClientJoinRequestSubmitted:{request.Id}",
            null);
    }

    public async Task NotifyEmployeeJoinRequestSubmittedAsync(JoinRequest request)
    {
        List<string> recipients = await GetSystemAdminRecipientsAsync();
        if (recipients.Count == 0)
        {
            return;
        }

        string title = "طلب انضمام موظف جديد";
        string message = $"تم تقديم طلب انضمام جديد كموظف من {request.FullName} ({request.Email}).";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.EmployeeJoinRequestSubmitted,
            title,
            message,
            $"EmployeeJoinRequestSubmitted:{request.Id}",
            null);
    }

    public async Task NotifyClientTopUpSubmittedAsync(
        int clientId,
        int? networkId,
        decimal amount,
        string sourceDisplayName,
        string? actorDisplayName)
    {
        List<string> recipients = await GetCompanyAdminRecipientsAsync(networkId);
        if (recipients.Count == 0)
        {
            return;
        }

        string title = "تغذية رصيد مشترك";
        string message = $"تمت تغذية رصيد المشترك #{clientId} بمبلغ {amount:N0} ل.س عبر {sourceDisplayName}" +
                      $"{(string.IsNullOrWhiteSpace(actorDisplayName) ? string.Empty : $" بواسطة {actorDisplayName}")}.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.ClientTopUpSubmitted,
            title,
            message,
            $"ClientTopUpSubmitted:{clientId}:{DateTime.Now:yyyyMMddHHmmss}",
            networkId);
    }

    public async Task NotifyCollectionPointTopUpRequestSubmittedAsync(
        CollectionPointTopUpRequest request,
        string? requesterDisplayName)
    {
        List<string> recipients;
        int? networkId = request.TargetNetworkId ?? request.CollectionPointAccount?.NetworkId;

        if (request.RequestTargetType == CollectionPointTopUpTarget.SystemAdmin)
        {
            recipients = await GetSystemAdminRecipientsAsync();
        }
        else
        {
            recipients = await GetCompanyAdminRecipientsAsync(networkId);
        }

        if (recipients.Count == 0)
        {
            return;
        }

        string title = "طلب تغذية رصيد من نقطة تحصيل";
        string message = $"تم تقديم طلب تغذية رصيد بمبلغ {request.Amount:N0} ل.س من نقطة التحصيل {(requesterDisplayName ?? request.RequestedByUserId)}.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.CollectionPointTopUpRequestSubmitted,
            title,
            message,
            $"CollectionPointTopUpRequestSubmitted:{request.Id}",
            networkId);
    }

    /// <summary>
    /// إشعار مدير الشركة أو مستخدم نقطة التحصيل المختارة بطلب تغذية رصيد من المشترك.
    /// </summary>
    public async Task NotifyClientWalletTopUpRequestSubmittedAsync(
        ClientWalletTopUpRequest request,
        string? clientDisplayName)
    {
        List<string> recipients;
        int? networkId = request.NetworkId;

        if (request.RecipientTarget == ClientWalletTopUpRecipientTarget.CompanyManager)
        {
            recipients = await GetCompanyAdminRecipientsAsync(networkId);
        }
        else
        {
            recipients = new List<string>();
            if (request.TargetCollectionPointAccountId.HasValue)
            {
                string? userId = await _context.CollectionPointAccounts
                    .AsNoTracking()
                    .Where(a => a.Id == request.TargetCollectionPointAccountId.Value)
                    .Select(a => a.UserId)
                    .FirstOrDefaultAsync();
                if (!string.IsNullOrWhiteSpace(userId))
                {
                    recipients.Add(userId);
                }
            }
        }

        if (recipients.Count == 0)
        {
            return;
        }

        string title = "طلب تغذية رصيد من مشترك";
        string targetLabel = request.RecipientTarget == ClientWalletTopUpRecipientTarget.CompanyManager
            ? "مدير الشركة"
            : "نقطة التحصيل";
        string message = $"تم تقديم طلب تغذية رصيد بمبلغ {request.Amount:N0} ل.س من المشترك {(clientDisplayName ?? $"#{request.ClientId}")} — الجهة: {targetLabel}.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.ClientWalletTopUpRequestSubmitted,
            title,
            message,
            $"ClientWalletTopUpRequestSubmitted:{request.Id}",
            networkId);
    }

    public async Task NotifyMaintenanceInvoiceIssuedAsync(
        MaintenanceInvoice invoice,
        string? clientUserName,
        string? clientName)
    {
        var recipients = new List<string>();
        var userId = await _context.Users
            .AsNoTracking()
            .Where(u => u.ClientId == invoice.ClientId)
            .Select(u => u.Id)
            .FirstOrDefaultAsync();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            recipients.Add(userId);
        }

        if (recipients.Count == 0)
        {
            return;
        }

        var title = "فاتورة صيانة جديدة";
        var message =
            $"تم إصدار فاتورة صيانة رقم #{invoice.Id} بمبلغ {invoice.GrossAmount:N0} ل.س على حساب {(clientName ?? clientUserName ?? $"#{invoice.ClientId}")}.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.MaintenanceInvoiceIssued,
            title,
            message,
            $"MaintenanceInvoiceIssued:{invoice.Id}",
            invoice.NetworkId);
    }

    public async Task NotifyMaintenanceInvoicePaidAsync(MaintenanceInvoice invoice)
    {
        var recipients = await GetCompanyAdminRecipientsAsync(invoice.NetworkId);
        if (recipients.Count == 0)
        {
            return;
        }

        var title = "تم سداد فاتورة صيانة";
        var message =
            $"تم سداد فاتورة الصيانة #{invoice.Id} بمبلغ {invoice.GrossAmount:N0} ل.س (صافي الشركة {invoice.NetAmountToCompany:N0} ل.س).";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.MaintenanceInvoicePaid,
            title,
            message,
            $"MaintenanceInvoicePaid:{invoice.Id}",
            invoice.NetworkId);
    }

    private async Task<List<string>> GetCompanyAdminRecipientsAsync(int? networkId)
    {
        HashSet<string> recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (networkId.HasValue)
        {
            string? managerUserId = await _context.Networks
                .AsNoTracking()
                .Where(n => n.Id == networkId.Value)
                .Select(n => n.ManagerUserId)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(managerUserId))
            {
                recipients.Add(managerUserId);
            }
        }

        List<string> roleRecipients = await GetUserIdsByRoleAsync(RoleNames.NetworkAdministrator, networkId);
        foreach (string userId in roleRecipients)
        {
            recipients.Add(userId);
        }

        return recipients.ToList();
    }

    private Task<List<string>> GetSystemAdminRecipientsAsync()
    {
        return GetUserIdsByRoleAsync(RoleNames.SystemAdministrator, null);
    }

    private async Task<List<string>> GetUserIdsByRoleAsync(string roleName, int? networkId)
    {
        string? roleId = await _context.Roles
            .AsNoTracking()
            .Where(r => r.Name == roleName)
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        if (string.IsNullOrWhiteSpace(roleId))
        {
            return new List<string>();
        }

        IQueryable<string> userIdsQuery = _context.UserRoles
            .AsNoTracking()
            .Where(ur => ur.RoleId == roleId)
            .Select(ur => ur.UserId)
            .Distinct();

        if (!networkId.HasValue)
        {
            return await userIdsQuery.ToListAsync();
        }

        return await _context.Users
            .AsNoTracking()
            .Where(u => u.NetworkId == networkId.Value)
            .Join(userIdsQuery, u => u.Id, id => id, (u, id) => id)
            .Distinct()
            .ToListAsync();
    }

    private async Task CreateNotificationsAsync(
        IEnumerable<string> recipientUserIds,
        NotificationType type,
        string title,
        string message,
        string keyPrefix,
        int? networkId)
    {
        DateTime now = DateTime.Now;
        List<string> recipients = recipientUserIds
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (recipients.Count == 0)
        {
            return;
        }

        List<UserNotification> rows = recipients.Select(userId => new UserNotification
        {
            Key = $"{keyPrefix}:{userId}:{Guid.NewGuid():N}",
            UserId = userId,
            NetworkId = networkId,
            Type = type,
            Title = title,
            Message = message,
            CreatedAt = now,
            IsRead = false
        }).ToList();

        _context.UserNotifications.AddRange(rows);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created {Count} notifications for {Type}", rows.Count, type);
    }
}
