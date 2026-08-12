using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services.Clients;

public interface IClientPendingApprovalQueryService
{
    Task<HashSet<int>> GetPendingClientIdsAsync(int selectedNetworkId, CancellationToken ct = default);

    Task<bool> IsPendingClientApprovalAsync(Client client, CancellationToken ct = default);
}

public sealed class ClientPendingApprovalQueryService(ApplicationDbContext context)
    : ApplicationServiceBase(context), IClientPendingApprovalQueryService
{
    public async Task<HashSet<int>> GetPendingClientIdsAsync(int selectedNetworkId, CancellationToken ct = default)
    {
        Network? selectedNetwork = await Db.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;

        List<string> notes = await Db.NetworkServiceRequests
            .AsNoTracking()
            .Where(r =>
                r.NetworkId == companyNetworkId &&
                r.Status == NetworkServiceRequestStatus.Pending &&
                r.FeatureKey == FeatureKeys.Clients &&
                r.Notes != null &&
                r.Notes.StartsWith("EMP_REQ:CLIENT_"))
            .Select(r => r.Notes!)
            .ToListAsync(ct);

        HashSet<int> ids = [];
        foreach (string note in notes)
        {
            if (EmployeeApprovalRequestHelper.TryParse(note, out EmployeeApprovalRequestKind kind, out int entityId, out _) &&
                (kind == EmployeeApprovalRequestKind.ClientCreate || kind == EmployeeApprovalRequestKind.ClientEdit))
            {
                ids.Add(entityId);
            }
        }

        return ids;
    }

    public async Task<bool> IsPendingClientApprovalAsync(Client client, CancellationToken ct = default)
    {
        if (!client.NetworkId.HasValue)
        {
            return false;
        }

        HashSet<int> pendingIds = await GetPendingClientIdsAsync(client.NetworkId.Value, ct);
        if (pendingIds.Contains(client.Id))
        {
            return true;
        }

        return string.Equals(
            client.ConnectionStatus,
            "معلق بانتظار موافقة مدير الشركة",
            StringComparison.OrdinalIgnoreCase);
    }
}
