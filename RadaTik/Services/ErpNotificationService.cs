using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;
using RadaTik.Security;

namespace RadaTik.Services;

public interface IErpNotificationService
{
    Task NotifyTaskAssignedAsync(CompanyEmployeeTask task, CancellationToken ct = default);
    Task NotifyRewardPenaltyPendingAsync(EmployeeRewardPenalty record, string? excludeUserId = null, CancellationToken ct = default);
    Task NotifyRewardPenaltyReviewedAsync(EmployeeRewardPenalty record, bool approved, CancellationToken ct = default);
}

public sealed class ErpNotificationService : IErpNotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ErpNotificationService> _logger;

    public ErpNotificationService(ApplicationDbContext context, ILogger<ErpNotificationService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task NotifyTaskAssignedAsync(CompanyEmployeeTask task, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(task.AssignedToUserId))
        {
            return;
        }

        string title = "مهمة جديدة";
        string message = $"تم تعيين مهمة جديدة لك: {task.Title}";

        await CreateNotificationAsync(
            task.AssignedToUserId,
            NotificationType.ErpTaskAssigned,
            title,
            message,
            $"ErpTaskAssigned:{task.Id}",
            task.CompanyNetworkId,
            ct);
    }

    public async Task NotifyRewardPenaltyPendingAsync(
        EmployeeRewardPenalty record,
        string? excludeUserId = null,
        CancellationToken ct = default)
    {
        List<string> recipients = await GetCompanyAdminRecipientsAsync(record.CompanyNetworkId, ct);
        if (!string.IsNullOrWhiteSpace(excludeUserId))
        {
            recipients = recipients
                .Where(id => !string.Equals(id, excludeUserId, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (recipients.Count == 0)
        {
            return;
        }

        string typeLabel = record.Type == EmployeeRewardPenaltyType.Reward ? "مكافأة" : "عقوبة";
        string title = $"{typeLabel} بانتظار الاعتماد";
        string message = $"طلب {typeLabel} بمبلغ {record.Amount:N0} بانتظار الاعتماد.";

        await CreateNotificationsAsync(
            recipients,
            NotificationType.ErpRewardPenaltyPending,
            title,
            message,
            $"ErpRewardPenaltyPending:{record.Id}",
            record.CompanyNetworkId,
            ct);
    }

    public async Task NotifyRewardPenaltyReviewedAsync(
        EmployeeRewardPenalty record,
        bool approved,
        CancellationToken ct = default)
    {
        PayrollEmployee? payrollEmployee = record.PayrollEmployee
            ?? await _context.PayrollEmployees.AsNoTracking()
                .FirstOrDefaultAsync(e => e.Id == record.PayrollEmployeeId, ct);

        if (payrollEmployee == null || string.IsNullOrWhiteSpace(payrollEmployee.ApplicationUserId))
        {
            return;
        }

        string typeLabel = record.Type == EmployeeRewardPenaltyType.Reward ? "مكافأة" : "عقوبة";
        string title = approved
            ? $"تم اعتماد {typeLabel}"
            : $"تم رفض {typeLabel}";
        string message = approved
            ? $"تم اعتماد {typeLabel} بمبلغ {record.Amount:N0}."
            : $"تم رفض {typeLabel} بمبلغ {record.Amount:N0}.";

        await CreateNotificationAsync(
            payrollEmployee.ApplicationUserId,
            NotificationType.ErpRewardPenaltyReviewed,
            title,
            message,
            $"ErpRewardPenaltyReviewed:{record.Id}",
            record.CompanyNetworkId,
            ct);
    }

    private async Task<List<string>> GetCompanyAdminRecipientsAsync(int companyNetworkId, CancellationToken ct)
    {
        HashSet<string> recipients = new(StringComparer.OrdinalIgnoreCase);

        string? managerUserId = await _context.Networks.AsNoTracking()
            .Where(n => n.Id == companyNetworkId)
            .Select(n => n.ManagerUserId)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(managerUserId))
        {
            recipients.Add(managerUserId);
        }

        string? roleId = await _context.Roles.AsNoTracking()
            .Where(r => r.Name == RoleNames.NetworkAdministrator)
            .Select(r => r.Id)
            .FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(roleId))
        {
            List<string> roleUserIds = await _context.UserRoles.AsNoTracking()
                .Where(ur => ur.RoleId == roleId)
                .Select(ur => ur.UserId)
                .Distinct()
                .ToListAsync(ct);

            List<string> networkUserIds = await _context.Users.AsNoTracking()
                .Where(u => u.NetworkId == companyNetworkId && roleUserIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync(ct);

            foreach (string userId in networkUserIds)
            {
                recipients.Add(userId);
            }
        }

        return recipients.ToList();
    }

    private async Task CreateNotificationAsync(
        string recipientUserId,
        NotificationType type,
        string title,
        string message,
        string keyPrefix,
        int? networkId,
        CancellationToken ct)
    {
        await CreateNotificationsAsync(
            new[] { recipientUserId },
            type,
            title,
            message,
            keyPrefix,
            networkId,
            ct);
    }

    private async Task CreateNotificationsAsync(
        IEnumerable<string> recipientUserIds,
        NotificationType type,
        string title,
        string message,
        string keyPrefix,
        int? networkId,
        CancellationToken ct)
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
            IsRead = false,
        }).ToList();

        _context.UserNotifications.AddRange(rows);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Created {Count} ERP notifications for {Type}", rows.Count, type);
    }
}
