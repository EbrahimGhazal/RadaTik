using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Security;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.PricingPolicies;

namespace RadaTik.Services.Approvals;

public sealed class EmployeeServiceApprovalRequestService(ApplicationDbContext context)
    : IEmployeeServiceApprovalRequestService
{
    public async Task<int> CreatePendingAsync(
        int selectedNetworkId,
        string actorUserId,
        string featureKey,
        string notes,
        decimal expectedChargeAmountSyp = 0m,
        CancellationToken ct = default)
    {
        Network? selectedNetwork = await context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

        NetworkServiceRequest request = new()
        {
            NetworkId = companyNetworkId,
            FeatureKey = featureKey,
            BillingPeriod = PricingBillingPeriod.OneTime,
            AmountSYP = Math.Max(0m, WalletMath.CeilSyp(expectedChargeAmountSyp)),
            AmountUSD = 0m,
            Currency = PricingCurrency.SYP_New,
            Status = NetworkServiceRequestStatus.Pending,
            RequestedByUserId = actorUserId,
            RequestedAt = DateTime.Now,
            Notes = notes
        };
        context.NetworkServiceRequests.Add(request);
        await context.SaveChangesAsync(ct);
        await NotifyManagersAsync(companyNetworkId, featureKey, request.Id, ct);
        return request.Id;
    }

    private async Task NotifyManagersAsync(int companyNetworkId, string featureKey, int requestId, CancellationToken ct)
    {
        HashSet<string> recipients = new(StringComparer.OrdinalIgnoreCase);
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(context, companyNetworkId);

        string? managerUserId = await context.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId)
            .Select(n => n.ManagerUserId)
            .FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(managerUserId))
        {
            recipients.Add(managerUserId);
        }

        List<string> roleUserIds = await context.Users
            .AsNoTracking()
            .Where(u => u.NetworkId.HasValue && companyScope.Contains(u.NetworkId.Value))
            .Join(context.UserRoles.AsNoTracking(), u => u.Id, ur => ur.UserId, (u, ur) => new { u.Id, ur.RoleId })
            .Join(
                context.Roles.AsNoTracking().Where(r => r.Name == RoleNames.NetworkAdministrator),
                x => x.RoleId,
                r => r.Id,
                (x, _) => x.Id)
            .Distinct()
            .ToListAsync(ct);

        foreach (string uid in roleUserIds)
        {
            recipients.Add(uid);
        }

        if (recipients.Count == 0)
        {
            return;
        }

        string actionLabel = featureKey == FeatureKeys.Clients ? "المشترك" : "الخدمة";
        DateTime now = DateTime.Now;
        IEnumerable<UserNotification> rows = recipients.Select(uid => new UserNotification
        {
            Key = $"EmployeeApprovalPending:{featureKey}:{requestId}:{uid}:{Guid.NewGuid():N}",
            UserId = uid,
            NetworkId = companyNetworkId,
            Type = NotificationType.SubscriptionExpiring,
            Title = "طلب موافقة جديد من موظف",
            Message = $"يوجد طلب {actionLabel} من موظف بانتظار اعتمادك.",
            CreatedAt = now,
            IsRead = false
        });

        context.UserNotifications.AddRange(rows);
        await context.SaveChangesAsync(ct);
    }
}
