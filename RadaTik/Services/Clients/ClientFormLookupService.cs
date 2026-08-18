using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;

namespace RadaTik.Services.Clients;

public sealed class ClientFormLookupService(ApplicationDbContext context)
    : ApplicationServiceBase(context), IClientFormLookupService
{
    public async Task<IReadOnlyList<ClientFormProfileOption>> GetProfilesByServerAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default)
    {
        bool serverInNetwork = await Db.MikroTikServers
            .AnyAsync(s => s.Id == serverId && s.NetworkId == networkId, ct);
        if (!serverInNetwork)
        {
            return Array.Empty<ClientFormProfileOption>();
        }

        return await Db.Profiles
            .AsNoTracking()
            .Where(p => p.MikroTikServerId == serverId && p.IsActive)
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .Select(p => new ClientFormProfileOption { Id = p.Id, Name = p.Name })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClientFormReceiverOption>> GetReceiversByServerAsync(
        int serverId,
        int networkId,
        CancellationToken ct = default)
    {
        bool serverInNetwork = await Db.MikroTikServers
            .AsNoTracking()
            .AnyAsync(s => s.Id == serverId && s.NetworkId == networkId, ct);
        if (!serverInNetwork)
        {
            return Array.Empty<ClientFormReceiverOption>();
        }

        return await Db.Receivers
            .AsNoTracking()
            .Where(r => r.NetworkId == networkId && r.Sector.MikroTikServerId == serverId)
            .OrderBy(r => r.Name)
            .Select(r => new ClientFormReceiverOption
            {
                Id = r.Id,
                Name = r.Name,
                SectorName = r.Sector.Name
            })
            .ToListAsync(ct);
    }
}
