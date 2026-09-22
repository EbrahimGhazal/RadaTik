using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

/// <summary>
/// إدارة حضور PPPoE لمشترك واحد على عدة سيرفرات بدون استنساخ صف Client.
/// </summary>
public static class ClientServerPresenceHelper
{
    public static async Task EnsureHomePresenceAsync(
        ApplicationDbContext db,
        Client client,
        CancellationToken ct = default)
    {
        if (!client.MikroTikServerId.HasValue || client.MikroTikServerId.Value <= 0)
        {
            return;
        }

        int homeId = client.MikroTikServerId.Value;
        await UpsertPresenceAsync(db, client.Id, homeId, ClientServerPresenceRole.Primary, ct);
        if (!client.ActiveServingServerId.HasValue)
        {
            client.ActiveServingServerId = homeId;
        }
    }

    public static async Task UpsertPresenceAsync(
        ApplicationDbContext db,
        int clientId,
        int serverId,
        ClientServerPresenceRole role,
        CancellationToken ct = default)
    {
        if (clientId <= 0 || serverId <= 0)
        {
            return;
        }

        ClientServerPresence? existing = await db.ClientServerPresences
            .FirstOrDefaultAsync(p => p.ClientId == clientId && p.MikroTikServerId == serverId, ct);

        if (existing is null)
        {
            db.ClientServerPresences.Add(new ClientServerPresence
            {
                ClientId = clientId,
                MikroTikServerId = serverId,
                Role = role,
                CreatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        if (role == ClientServerPresenceRole.Primary
            || existing.Role != ClientServerPresenceRole.Primary)
        {
            existing.Role = role;
        }
    }

    public static async Task<int> LinkFailoverPresenceAsync(
        ApplicationDbContext db,
        int networkId,
        int targetServerId,
        IReadOnlyCollection<int> placedClientIds,
        CancellationToken ct = default)
    {
        if (placedClientIds.Count == 0)
        {
            return 0;
        }

        List<Client> clients = await db.Clients
            .Where(c => c.NetworkId == networkId && placedClientIds.Contains(c.Id))
            .ToListAsync(ct);

        DateTime now = DateTime.Now;
        int linked = 0;
        foreach (Client client in clients)
        {
            await EnsureHomePresenceAsync(db, client, ct);
            await UpsertPresenceAsync(
                db,
                client.Id,
                targetServerId,
                ClientServerPresenceRole.Standby,
                ct);
            client.ActiveServingServerId = targetServerId;
            client.LastUpdated = now;
            client.IsCrossServerDuplicate = false;
            linked++;
        }

        await db.SaveChangesAsync(ct);
        return linked;
    }

    public static async Task SyncHomePresenceAfterMoveAsync(
        ApplicationDbContext db,
        int networkId,
        int targetServerId,
        IReadOnlyCollection<int> placedClientIds,
        CancellationToken ct = default)
    {
        if (placedClientIds.Count == 0)
        {
            return;
        }

        List<Client> clients = await db.Clients
            .Include(c => c.ServerPresences)
            .Where(c => c.NetworkId == networkId && placedClientIds.Contains(c.Id))
            .ToListAsync(ct);

        DateTime now = DateTime.Now;
        foreach (Client client in clients)
        {
            client.MikroTikServerId = targetServerId;
            client.ActiveServingServerId = targetServerId;
            client.LastUpdated = now;
            client.IsCrossServerDuplicate = false;

            List<ClientServerPresence> toRemove = client.ServerPresences
                .Where(p => p.MikroTikServerId != targetServerId)
                .ToList();
            if (toRemove.Count > 0)
            {
                db.ClientServerPresences.RemoveRange(toRemove);
            }

            await UpsertPresenceAsync(
                db,
                client.Id,
                targetServerId,
                ClientServerPresenceRole.Primary,
                ct);
        }

        await db.SaveChangesAsync(ct);
    }

    public static async Task<IReadOnlyList<int>> GetPppoeServerIdsAsync(
        ApplicationDbContext db,
        int clientId,
        int? homeServerId,
        int? activeServingServerId,
        CancellationToken ct = default)
    {
        HashSet<int> ids = (await db.ClientServerPresences
            .AsNoTracking()
            .Where(p => p.ClientId == clientId)
            .Select(p => p.MikroTikServerId)
            .ToListAsync(ct))
            .ToHashSet();

        if (homeServerId is int home && home > 0)
        {
            ids.Add(home);
        }

        if (activeServingServerId is int active && active > 0)
        {
            ids.Add(active);
        }

        return ids.OrderBy(id => id).ToList();
    }

    public static async Task<ClientOperationOutcome> EndFailoverAsync(
        ApplicationDbContext db,
        IMikroTikPppoeUserService mikroTik,
        int clientId,
        int networkId,
        bool removeStandbyAccounts,
        CancellationToken ct = default)
    {
        Client? client = await db.Clients
            .Include(c => c.ServerPresences)
            .FirstOrDefaultAsync(c => c.Id == clientId && c.NetworkId == networkId, ct);
        if (client is null)
        {
            return ClientOperationOutcome.NotFoundClient();
        }

        if (!client.MikroTikServerId.HasValue || client.MikroTikServerId.Value <= 0)
        {
            return ClientOperationOutcome.Fail("لا يوجد برج أساسي محدد لهذا المشترك.");
        }

        int homeId = client.MikroTikServerId.Value;

        List<string> errors = [];
        int removed = 0;
        if (removeStandbyAccounts && !string.IsNullOrWhiteSpace(client.UserName))
        {
            List<ClientServerPresence> standbys = client.ServerPresences
                .Where(p => p.MikroTikServerId != homeId)
                .ToList();

            foreach (IGrouping<int, ClientServerPresence> group in standbys.GroupBy(p => p.MikroTikServerId))
            {
                BulkDeletePppoeUsersResult deleteResult = await mikroTik.DeletePPPoEUsersFromServerAsync(
                    group.Key,
                    [client.UserName!.Trim()],
                    ct);
                if (!deleteResult.Success)
                {
                    errors.Add(deleteResult.Message ?? $"فشل حذف الحساب من السيرفر {group.Key}");
                    continue;
                }

                removed += deleteResult.DeletedCount;
                db.ClientServerPresences.RemoveRange(group);
            }
        }

        client.ActiveServingServerId = homeId;
        client.IsCrossServerDuplicate = false;
        client.LastUpdated = DateTime.Now;
        await UpsertPresenceAsync(db, client.Id, homeId, ClientServerPresenceRole.Primary, ct);
        await db.SaveChangesAsync(ct);

        string msg = removeStandbyAccounts
            ? $"تمت إعادة الخدمة إلى البرج الأساسي وحذف {removed} حساباً احتياطياً من MikroTik."
            : "تمت إعادة السيرفر الفعّال إلى البرج الأساسي مع الإبقاء على الحسابات الاحتياطية.";
        if (errors.Count > 0)
        {
            msg += " تحذيرات: " + string.Join("؛ ", errors.Take(3));
        }

        return ClientOperationOutcome.Success(msg);
    }

    public static async Task RenewExpirationOnAllServersAsync(
        ApplicationDbContext db,
        IMikroTikPppoeUserService mikroTik,
        Client client,
        DateTime expirationDate,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(client.UserName))
        {
            return;
        }

        IReadOnlyList<int> serverIds = await GetPppoeServerIdsAsync(
            db,
            client.Id,
            client.MikroTikServerId,
            client.ActiveServingServerId,
            ct);

        if (serverIds.Count == 0 && client.MikroTikServerId is int only && only > 0)
        {
            serverIds = [only];
        }

        List<string> errors = [];
        foreach (int serverId in serverIds)
        {
            try
            {
                await mikroTik.RenewPPPoESubscription(client.UserName, serverId, expirationDate);
            }
            catch (Exception ex)
            {
                errors.Add($"سيرفر {serverId}: {ex.Message}");
            }
        }

        if (serverIds.Count > 0 && errors.Count == serverIds.Count)
        {
            throw new InvalidOperationException(string.Join("؛ ", errors.Take(3)));
        }
    }
}
