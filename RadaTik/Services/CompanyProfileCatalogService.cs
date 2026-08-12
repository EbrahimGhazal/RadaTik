using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services;

public sealed class CompanyProfileCatalogService : ICompanyProfileCatalogService
{
    private readonly ApplicationDbContext _context;
    private readonly IMikroTikProfilesService _mikroTikService;
    private readonly ILogger<CompanyProfileCatalogService> _logger;

    public CompanyProfileCatalogService(
        ApplicationDbContext context,
        IMikroTikProfilesService mikroTikService,
        ILogger<CompanyProfileCatalogService> logger)
    {
        _context = context;
        _mikroTikService = mikroTikService;
        _logger = logger;
    }

    public sealed class CatalogOperationResult
    {
        public bool Success { get; init; }
        public string? ErrorMessage { get; init; }
        public int? CatalogId { get; init; }
        public int DeployedCount { get; init; }
        public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();
    }

    public async Task<CatalogOperationResult> CreateCatalogAndDeployAsync(
        Profile template,
        IReadOnlyList<int> serverIds,
        int selectedNetworkId,
        CancellationToken cancellationToken = default)
    {
        if (serverIds.Count == 0)
        {
            return new CatalogOperationResult
            {
                Success = false,
                ErrorMessage = "اختر سيرفراً واحداً على الأقل لنشر البروفايل."
            };
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(selectedNetworkId, cancellationToken);
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        bool nameExists = await _context.CompanyProfileCatalogs
            .AsNoTracking()
            .AnyAsync(c => c.CompanyNetworkId == companyNetworkId && c.Name == template.Name, cancellationToken);
        if (nameExists)
        {
            CompanyProfileCatalog? existing = await _context.CompanyProfileCatalogs
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.CompanyNetworkId == companyNetworkId && c.Name == template.Name, cancellationToken);
            return new CatalogOperationResult
            {
                Success = false,
                ErrorMessage = $"البروفايل «{template.Name}» موجود في كتالوج الشركة. استخدم «نشر على سيرفرات» لإضافته على سيرفرات أخرى.",
                CatalogId = existing?.Id
            };
        }

        List<MikroTikServer> servers = await _context.MikroTikServers
            .AsNoTracking()
            .Where(s => serverIds.Contains(s.Id) && s.IsActive && s.NetworkId.HasValue && companyScope.Contains(s.NetworkId.Value))
            .ToListAsync(cancellationToken);

        if (servers.Count != serverIds.Count)
        {
            return new CatalogOperationResult
            {
                Success = false,
                ErrorMessage = "أحد السيرفرات المحددة غير صالح أو لا يتبع نطاق شركتك."
            };
        }

        CompanyProfileCatalog catalog = MapToCatalog(template, companyNetworkId);
        List<Profile> deployedProfiles = new();
        List<string> mikrotikRollback = new();

        try
        {
            foreach (MikroTikServer server in servers)
            {
                bool alreadyOnServer = await _context.Profiles
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.MikroTikServerId == server.Id &&
                        p.Name == template.Name &&
                        p.NetworkId.HasValue &&
                        companyScope.Contains(p.NetworkId.Value),
                        cancellationToken);
                if (alreadyOnServer)
                {
                    return new CatalogOperationResult
                    {
                        Success = false,
                        ErrorMessage = $"البروفايل «{template.Name}» موجود مسبقاً على السيرفر «{server.Name}»."
                    };
                }

                Profile deployment = MapToDeployment(catalog, template, server);
                string mikrotikId = await _mikroTikService.AddProfileToMikroTik(server.Id, deployment);
                mikrotikRollback.Add($"{server.Id}|{deployment.Name}");

                deployment.MikroTikProfileId = mikrotikId;
                deployment.IsSyncedWithMikroTik = true;
                deployment.LastSyncDate = DateTime.Now;
                deployedProfiles.Add(deployment);
            }

            _context.CompanyProfileCatalogs.Add(catalog);
            await _context.SaveChangesAsync(cancellationToken);

            foreach (Profile deployment in deployedProfiles)
            {
                deployment.CompanyProfileCatalogId = catalog.Id;
                _context.Profiles.Add(deployment);
            }

            await _context.SaveChangesAsync(cancellationToken);

            return new CatalogOperationResult
            {
                Success = true,
                CatalogId = catalog.Id,
                DeployedCount = deployedProfiles.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل إنشاء كتالوج البروفايل {Name}", template.Name);
            await RollbackMikroTikProfilesAsync(mikrotikRollback, cancellationToken);
            return new CatalogOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<CatalogOperationResult> DeployCatalogToServersAsync(
        int catalogId,
        IReadOnlyList<int> serverIds,
        int selectedNetworkId,
        CancellationToken cancellationToken = default)
    {
        if (serverIds.Count == 0)
        {
            return new CatalogOperationResult
            {
                Success = false,
                ErrorMessage = "اختر سيرفراً واحداً على الأقل."
            };
        }

        int companyNetworkId = await ResolveCompanyNetworkIdAsync(selectedNetworkId, cancellationToken);
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        CompanyProfileCatalog? catalog = await _context.CompanyProfileCatalogs
            .FirstOrDefaultAsync(c => c.Id == catalogId && c.CompanyNetworkId == companyNetworkId, cancellationToken);
        if (catalog == null)
        {
            return new CatalogOperationResult { Success = false, ErrorMessage = "كتالوج البروفايل غير موجود." };
        }

        HashSet<int> alreadyDeployed = await _context.Profiles
            .AsNoTracking()
            .Where(p => p.CompanyProfileCatalogId == catalogId)
            .Select(p => p.MikroTikServerId)
            .ToHashSetAsync(cancellationToken);

        List<MikroTikServer> servers = await _context.MikroTikServers
            .AsNoTracking()
            .Where(s => serverIds.Contains(s.Id) && s.IsActive && s.NetworkId.HasValue && companyScope.Contains(s.NetworkId.Value))
            .ToListAsync(cancellationToken);

        List<string> warnings = new();
        List<Profile> newDeployments = new();
        List<string> mikrotikRollback = new();

        try
        {
            foreach (MikroTikServer server in servers)
            {
                if (alreadyDeployed.Contains(server.Id))
                {
                    warnings.Add($"تم تخطي «{server.Name}» — منشور مسبقاً.");
                    continue;
                }

                bool nameConflict = await _context.Profiles
                    .AsNoTracking()
                    .AnyAsync(p =>
                        p.MikroTikServerId == server.Id &&
                        p.Name == catalog.Name &&
                        (p.CompanyProfileCatalogId == null || p.CompanyProfileCatalogId != catalogId),
                        cancellationToken);
                if (nameConflict)
                {
                    warnings.Add($"تعذر النشر على «{server.Name}» — يوجد بروفايل بنفس الاسم.");
                    continue;
                }

                Profile deployment = MapToDeployment(catalog, null, server);
                deployment.CompanyProfileCatalogId = catalog.Id;
                string mikrotikId = await _mikroTikService.AddProfileToMikroTik(server.Id, deployment);
                mikrotikRollback.Add($"{server.Id}|{deployment.Name}");

                deployment.MikroTikProfileId = mikrotikId;
                deployment.IsSyncedWithMikroTik = true;
                deployment.LastSyncDate = DateTime.Now;
                newDeployments.Add(deployment);
            }

            if (newDeployments.Count == 0)
            {
                return new CatalogOperationResult
                {
                    Success = false,
                    ErrorMessage = "لم يتم نشر البروفايل على أي سيرفر جديد.",
                    Warnings = warnings,
                    CatalogId = catalogId
                };
            }

            _context.Profiles.AddRange(newDeployments);
            catalog.UpdatedDate = DateTime.Now;
            await _context.SaveChangesAsync(cancellationToken);

            return new CatalogOperationResult
            {
                Success = true,
                CatalogId = catalogId,
                DeployedCount = newDeployments.Count,
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "فشل نشر كتالوج {CatalogId}", catalogId);
            await RollbackMikroTikProfilesAsync(mikrotikRollback, cancellationToken);
            return new CatalogOperationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Warnings = warnings,
                CatalogId = catalogId
            };
        }
    }

    public async Task<List<MikroTikServer>> GetDeployableServersAsync(
        int selectedNetworkId,
        int? catalogId,
        CancellationToken cancellationToken = default)
    {
        int companyNetworkId = await ResolveCompanyNetworkIdAsync(selectedNetworkId, cancellationToken);
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_context, companyNetworkId);

        return await _context.MikroTikServers
            .AsNoTracking()
            .Where(s => s.IsActive && s.NetworkId.HasValue && companyScope.Contains(s.NetworkId.Value))
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
    }

    private async Task RollbackMikroTikProfilesAsync(
        IReadOnlyList<string> rollbackKeys,
        CancellationToken cancellationToken)
    {
        foreach (string key in rollbackKeys)
        {
            string[] parts = key.Split('|', 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int serverId))
            {
                continue;
            }

            try
            {
                await _mikroTikService.DeleteProfileFromMikroTik(serverId, parts[1]);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "تعذر التراجع عن بروفايل MikroTik {Profile} على السيرفر {ServerId}", parts[1], serverId);
            }
        }
    }

    private static CompanyProfileCatalog MapToCatalog(Profile template, int companyNetworkId) =>
        new()
        {
            CompanyNetworkId = companyNetworkId,
            Name = template.Name,
            Description = template.Description,
            Type = template.Type,
            BillingCycle = template.BillingCycle,
            Price = template.Price,
            VATPercentage = template.VATPercentage,
            DownloadSpeed = template.DownloadSpeed,
            DownloadSpeedUnit = template.DownloadSpeedUnit,
            UploadSpeed = template.UploadSpeed,
            UploadSpeedUnit = template.UploadSpeedUnit,
            DataLimit = template.DataLimit,
            TimeLimit = template.TimeLimit,
            IPTVDevices = template.IPTVDevices,
            IsDataCapped = template.IsDataCapped,
            IsTimeCapped = template.IsTimeCapped,
            MaxUsers = template.MaxUsers,
            MinDevices = template.MinDevices,
            MaxDevices = template.MaxDevices,
            AllowedPorts = template.AllowedPorts,
            AllowedAddresses = template.AllowedAddresses,
            Features = template.Features,
            IsActive = template.IsActive,
            IsForNewClients = template.IsForNewClients,
            DisplayOrder = template.DisplayOrder,
            MikroTikLocalAddress = template.MikroTikLocalAddress,
            MikroTikRemoteAddress = template.MikroTikRemoteAddress,
            MikroTikRateLimit = template.MikroTikRateLimit,
            MikroTikOnlyOne = template.MikroTikOnlyOne,
            MikroTikService = template.MikroTikService,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };

    private static Profile MapToDeployment(
        CompanyProfileCatalog catalog,
        Profile? templateOverrides,
        MikroTikServer server)
    {
        Profile source = templateOverrides ?? new Profile
        {
            Name = catalog.Name,
            Description = catalog.Description,
            Type = catalog.Type,
            BillingCycle = catalog.BillingCycle,
            Price = catalog.Price,
            VATPercentage = catalog.VATPercentage,
            DownloadSpeed = catalog.DownloadSpeed,
            DownloadSpeedUnit = catalog.DownloadSpeedUnit,
            UploadSpeed = catalog.UploadSpeed,
            UploadSpeedUnit = catalog.UploadSpeedUnit,
            DataLimit = catalog.DataLimit,
            TimeLimit = catalog.TimeLimit,
            IPTVDevices = catalog.IPTVDevices,
            IsDataCapped = catalog.IsDataCapped,
            IsTimeCapped = catalog.IsTimeCapped,
            MaxUsers = catalog.MaxUsers,
            MinDevices = catalog.MinDevices,
            MaxDevices = catalog.MaxDevices,
            AllowedPorts = catalog.AllowedPorts,
            AllowedAddresses = catalog.AllowedAddresses,
            Features = catalog.Features,
            IsActive = catalog.IsActive,
            IsForNewClients = catalog.IsForNewClients,
            DisplayOrder = catalog.DisplayOrder,
            MikroTikLocalAddress = catalog.MikroTikLocalAddress,
            MikroTikRemoteAddress = catalog.MikroTikRemoteAddress,
            MikroTikRateLimit = catalog.MikroTikRateLimit,
            MikroTikOnlyOne = catalog.MikroTikOnlyOne,
            MikroTikService = catalog.MikroTikService
        };

        return new Profile
        {
            Name = source.Name,
            Description = source.Description,
            Type = source.Type,
            BillingCycle = source.BillingCycle,
            Price = source.Price,
            VATPercentage = source.VATPercentage,
            DownloadSpeed = source.DownloadSpeed,
            DownloadSpeedUnit = source.DownloadSpeedUnit,
            UploadSpeed = source.UploadSpeed,
            UploadSpeedUnit = source.UploadSpeedUnit,
            DataLimit = source.DataLimit,
            TimeLimit = source.TimeLimit,
            IPTVDevices = source.IPTVDevices,
            IsDataCapped = source.IsDataCapped,
            IsTimeCapped = source.IsTimeCapped,
            MaxUsers = source.MaxUsers,
            MinDevices = source.MinDevices,
            MaxDevices = source.MaxDevices,
            AllowedPorts = source.AllowedPorts,
            AllowedAddresses = source.AllowedAddresses,
            Features = source.Features,
            IsActive = source.IsActive,
            IsForNewClients = source.IsForNewClients,
            DisplayOrder = source.DisplayOrder,
            MikroTikLocalAddress = source.MikroTikLocalAddress,
            MikroTikRemoteAddress = source.MikroTikRemoteAddress,
            MikroTikRateLimit = source.MikroTikRateLimit,
            MikroTikOnlyOne = source.MikroTikOnlyOne,
            MikroTikService = source.MikroTikService,
            MikroTikServerId = server.Id,
            NetworkId = server.NetworkId,
            CreatedDate = DateTime.Now,
            UpdatedDate = DateTime.Now
        };
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken cancellationToken)
    {
        Network? selectedNetwork = await _context.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == networkId, cancellationToken);
        return selectedNetwork?.ParentNetworkId ?? networkId;
    }
}
