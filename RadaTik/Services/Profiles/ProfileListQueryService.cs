using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Models;
using RadaTik.ViewModels.Profile;

namespace RadaTik.Services.Profiles;

public sealed class ProfileListQueryService(
    ApplicationDbContext context,
    IProfileImportPricingService profileImportPricing)
    : ApplicationServiceBase(context), IProfileListQueryService
{
    public async Task<ProfileIndexPageModel?> BuildIndexPageAsync(
        int networkId,
        int? serverId,
        CancellationToken ct = default)
    {
        Network? selectedNetwork = await Db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == networkId, ct);
        if (selectedNetwork == null)
        {
            return null;
        }

        int companyNetworkId = selectedNetwork.ParentNetworkId ?? networkId;

        List<MikroTikServer> servers = await Db.MikroTikServers
            .Where(s => s.IsActive && s.NetworkId == networkId)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        IQueryable<Profile> profilesQuery = Db.Profiles
            .Where(p => p.NetworkId == networkId)
            .Include(p => p.MikroTikServer)
            .Include(p => p.CompanyProfileCatalog);

        if (serverId.HasValue)
        {
            profilesQuery = profilesQuery.Where(p => p.MikroTikServerId == serverId.Value);
        }

        List<Profile> profiles = await profilesQuery
            .OrderBy(p => p.MikroTikServerId)
            .ThenBy(p => p.DisplayOrder)
            .ThenBy(p => p.Name)
            .ToListAsync(ct);

        List<CompanyCatalogSummaryItem> catalogs = await Db.CompanyProfileCatalogs
            .AsNoTracking()
            .Where(c => c.CompanyNetworkId == companyNetworkId)
            .OrderBy(c => c.DisplayOrder)
            .ThenBy(c => c.Name)
            .Select(c => new CompanyCatalogSummaryItem
            {
                Id = c.Id,
                Name = c.Name,
                DeployedCount = c.Deployments.Count
            })
            .ToListAsync(ct);

        return new ProfileIndexPageModel
        {
            Profiles = profiles,
            Servers = servers,
            SelectedServerId = serverId,
            ProfileImportUnitPrice = await profileImportPricing.GetProfileImportUnitPriceAsync(ct),
            CompanyCatalogs = catalogs,
            TotalProfiles = profiles.Count,
            ActiveProfiles = profiles.Count(p => p.IsActive),
            SyncedProfiles = profiles.Count(p => p.IsSyncedWithMikroTik)
        };
    }
}
