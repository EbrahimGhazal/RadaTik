using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Services.MikroTik;

namespace RadaTik.Services.Clients;

public sealed class ClientLiveConnectionRefreshService(
    ApplicationDbContext db,
    IMikroTikPppoeUserService mikroTik,
    IClientLiveConnectionStore store,
    IServiceScopeFactory scopeFactory,
    ILogger<ClientLiveConnectionRefreshService> logger)
    : IClientLiveConnectionRefreshService
{
    public static readonly TimeSpan RequestWaitBudget = TimeSpan.FromSeconds(1);
    public static readonly TimeSpan ForceRefreshWaitBudget = TimeSpan.FromSeconds(4);

    private readonly ApplicationDbContext _db = db;
    private readonly IMikroTikPppoeUserService _mikroTik = mikroTik;
    private readonly IClientLiveConnectionStore _store = store;
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<ClientLiveConnectionRefreshService> _logger = logger;

    public async Task RefreshAllActiveServersAsync(CancellationToken ct = default)
    {
        if (!await _store.TryEnterPollAsync(TimeSpan.Zero, ct))
        {
            return;
        }

        try
        {
            List<int> serverIds = await _db.MikroTikServers
                .AsNoTracking()
                .Where(s => s.IsActive)
                .Select(s => s.Id)
                .ToListAsync(ct);

            await PollServersAndRebuildAsync(serverIds, ensureCompanyScopeId: null, ct);
        }
        finally
        {
            _store.ExitPoll();
        }
    }

    public async Task RefreshCompanyAsync(
        int networkId,
        bool force,
        TimeSpan waitBudget,
        CancellationToken ct = default)
    {
        List<int> companyNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(
            _db,
            networkId,
            ct);
        int cacheScopeId = companyNetworkIds.Count > 0 ? companyNetworkIds[0] : networkId;

        if (!force
            && _store.TryGetCompanySnapshot(cacheScopeId, out CompanyLiveConnectionSnapshot? cached)
            && cached is { Ready: true })
        {
            return;
        }

        HashSet<int> serverIds = await LoadActiveServerIdsAsync(companyNetworkIds, ct);
        StartDetachedPoll(serverIds, cacheScopeId);
        await WaitForCompanyReadyAsync(cacheScopeId, waitBudget, ct);
    }

    private void StartDetachedPoll(IReadOnlyCollection<int> serverIds, int companyScopeId)
    {
        IReadOnlyList<int> capturedIds = serverIds.ToList();
        _ = Task.Run(async () =>
        {
            try
            {
                using IServiceScope scope = _scopeFactory.CreateScope();
                ClientLiveConnectionRefreshService inner =
                    scope.ServiceProvider.GetRequiredService<ClientLiveConnectionRefreshService>();
                await inner.RunPollExclusiveAsync(capturedIds, companyScopeId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل التحديث المنفصل لحالة الاتصال");
            }
        });
    }

    public async Task RunPollExclusiveAsync(
        IReadOnlyCollection<int> serverIds,
        int? companyScopeId,
        CancellationToken ct)
    {
        if (!await _store.TryEnterPollAsync(TimeSpan.Zero, ct))
        {
            return;
        }

        try
        {
            await PollServersAndRebuildAsync(serverIds, companyScopeId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "فشل جمع جلسات /ppp/active من سيرفرات MikroTik");
        }
        finally
        {
            _store.ExitPoll();
        }
    }

    private async Task PollServersAndRebuildAsync(
        IReadOnlyCollection<int> serverIds,
        int? ensureCompanyScopeId,
        CancellationToken ct)
    {
        IReadOnlyList<PppActiveSessionQueryResult> results =
            await _mikroTik.QueryActivePppSessionNamesByServerAsync(serverIds, ct);

        foreach (PppActiveSessionQueryResult result in results)
        {
            _store.SetServerResult(result.ServerId, result.Names, result.Succeeded);
        }

        HashSet<int> companyScopeIds = [];
        List<int> knownServerIds = results.Select(r => r.ServerId).Distinct().ToList();
        if (knownServerIds.Count > 0)
        {
            List<int?> networkIds = await _db.MikroTikServers
                .AsNoTracking()
                .Where(s => knownServerIds.Contains(s.Id) && s.NetworkId.HasValue)
                .Select(s => s.NetworkId)
                .Distinct()
                .ToListAsync(ct);

            foreach (int? networkId in networkIds)
            {
                if (!networkId.HasValue)
                {
                    continue;
                }

                List<int> scope = await PricingChargeHelper.GetCompanyScopeNetworkIdsForSelectedAsync(
                    _db,
                    networkId.Value,
                    ct);
                if (scope.Count > 0)
                {
                    companyScopeIds.Add(scope[0]);
                }
            }
        }

        if (ensureCompanyScopeId.HasValue)
        {
            companyScopeIds.Add(ensureCompanyScopeId.Value);
        }

        foreach (int companyScopeId in companyScopeIds)
        {
            await RebuildCompanySnapshotAsync(companyScopeId, ct);
        }
    }

    private async Task RebuildCompanySnapshotAsync(int companyScopeId, CancellationToken ct)
    {
        List<int> companyNetworkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(
            _db,
            companyScopeId);
        if (companyNetworkIds.Count == 0)
        {
            companyNetworkIds = [companyScopeId];
        }

        List<Client> clients = await _db.Clients
            .AsNoTracking()
            .Where(c =>
                c.NetworkId.HasValue
                && companyNetworkIds.Contains(c.NetworkId.Value)
                && c.UserName != null)
            .Select(c => new Client
            {
                Id = c.Id,
                UserName = c.UserName,
                MikroTikServerId = c.MikroTikServerId,
                IsActive = c.IsActive
            })
            .ToListAsync(ct);

        HashSet<int> serverIds = await LoadActiveServerIdsAsync(companyNetworkIds, ct);
        IReadOnlyDictionary<int, IReadOnlyCollection<string>> namesByServer =
            _store.GetUsableSessionNames(serverIds);
        Dictionary<int, IReadOnlyCollection<string>> normalized = namesByServer.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyCollection<string>)pair.Value
                .Select(ClientLiveConnectionMatcher.NormalizeUserName)
                .Where(name => name.Length > 0)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        HashSet<int> connectedIds = ClientLiveConnectionMatcher.Match(clients, normalized);
        _store.SetCompanySnapshot(new CompanyLiveConnectionSnapshot(
            companyScopeId,
            connectedIds,
            DateTimeOffset.UtcNow,
            Ready: true));
    }

    private async Task<HashSet<int>> LoadActiveServerIdsAsync(
        IReadOnlyCollection<int> companyNetworkIds,
        CancellationToken ct)
    {
        HashSet<int> serverIds = await _db.MikroTikServers
            .AsNoTracking()
            .Where(s =>
                s.IsActive
                && s.NetworkId.HasValue
                && companyNetworkIds.Contains(s.NetworkId.Value))
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        List<int> assigned = await _db.Clients
            .AsNoTracking()
            .Where(c =>
                c.NetworkId.HasValue
                && companyNetworkIds.Contains(c.NetworkId.Value)
                && c.MikroTikServerId.HasValue)
            .Select(c => c.MikroTikServerId!.Value)
            .Distinct()
            .ToListAsync(ct);

        HashSet<int> activeAssigned = await _db.MikroTikServers
            .AsNoTracking()
            .Where(s => s.IsActive && assigned.Contains(s.Id))
            .Select(s => s.Id)
            .ToHashSetAsync(ct);

        serverIds.UnionWith(activeAssigned);
        return serverIds;
    }

    private async Task WaitForCompanyReadyAsync(int companyScopeId, TimeSpan wait, CancellationToken ct)
    {
        if (wait <= TimeSpan.Zero)
        {
            return;
        }

        using CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(wait);
        while (!cts.IsCancellationRequested)
        {
            if (_store.TryGetCompanySnapshot(companyScopeId, out CompanyLiveConnectionSnapshot? snapshot)
                && snapshot is { Ready: true })
            {
                return;
            }

            try
            {
                await Task.Delay(150, cts.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }
}
