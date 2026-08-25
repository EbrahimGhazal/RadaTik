using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public static class ClientCrossServerDuplicate
{
    public static async Task RefreshRemainingAsync(
        ApplicationDbContext db,
        int? networkId,
        string? userName,
        int excludedClientId,
        CancellationToken ct = default)
    {
        if (networkId is null or <= 0 || string.IsNullOrWhiteSpace(userName))
        {
            return;
        }

        string normalized = userName.Trim().ToLowerInvariant();
        List<Client> remaining = await db.Clients
            .Where(c =>
                c.Id != excludedClientId
                && c.NetworkId == networkId
                && c.MikroTikServerId != null
                && c.UserName != null
                && c.UserName.ToLower() == normalized)
            .ToListAsync(ct);

        bool stillDuplicate = remaining.Count > 1;
        DateTime now = DateTime.Now;
        foreach (Client sibling in remaining)
        {
            if (sibling.IsCrossServerDuplicate == stillDuplicate)
            {
                continue;
            }

            sibling.IsCrossServerDuplicate = stillDuplicate;
            sibling.LastUpdated = now;
        }
    }

    /// <summary>
    /// إذا حُذف الحساب المكرر من برج MikroTik، يُحذف سجله الزائد من التطبيق ويُلغى تعليم الباقي.
    /// لا يُحذف السجل الوحيد المتبقي حتى لو اختفى من السيرفر.
    /// </summary>
    public static async Task<int> RemoveCopiesMissingFromServerAsync(
        ApplicationDbContext db,
        int networkId,
        int serverId,
        IEnumerable<string> liveUserNamesOnServer,
        CancellationToken ct = default)
    {
        HashSet<string> live = liveUserNamesOnServer
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<Client> markedOnServer = await db.Clients
            .Where(c =>
                c.NetworkId == networkId
                && c.MikroTikServerId == serverId
                && c.IsCrossServerDuplicate
                && c.UserName != null)
            .ToListAsync(ct);

        List<(int Id, string UserName)> removed = [];
        foreach (Client leftover in markedOnServer)
        {
            string userName = leftover.UserName!.Trim();
            if (live.Contains(userName))
            {
                continue;
            }

            string normalized = userName.ToLowerInvariant();
            bool hasSiblingOnAnotherTower = await db.Clients.AnyAsync(
                c =>
                    c.Id != leftover.Id
                    && c.NetworkId == networkId
                    && c.MikroTikServerId != null
                    && c.MikroTikServerId != serverId
                    && c.UserName != null
                    && c.UserName.ToLower() == normalized,
                ct);
            if (!hasSiblingOnAnotherTower)
            {
                continue;
            }

            removed.Add((leftover.Id, userName));
            db.Clients.Remove(leftover);
        }

        foreach ((int id, string userName) in removed)
        {
            await RefreshRemainingAsync(db, networkId, userName, id, ct);
        }

        return removed.Count;
    }
}
