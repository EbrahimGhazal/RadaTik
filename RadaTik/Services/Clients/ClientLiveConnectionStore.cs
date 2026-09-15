using System.Collections.Concurrent;

namespace RadaTik.Services.Clients;

public sealed class ClientLiveConnectionStore : IClientLiveConnectionStore, IDisposable
{
    private readonly ConcurrentDictionary<int, ServerPppSessionsSnapshot> _servers = new();
    private readonly ConcurrentDictionary<int, CompanyLiveConnectionSnapshot> _companies = new();
    private readonly SemaphoreSlim _pollLock = new(1, 1);

    public void SetServerResult(int serverId, IReadOnlyList<string> names, bool succeeded)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        if (succeeded)
        {
            _servers[serverId] = new ServerPppSessionsSnapshot(
                serverId,
                names,
                now,
                Succeeded: true,
                LastFailureAt: null);
            return;
        }

        if (_servers.TryGetValue(serverId, out ServerPppSessionsSnapshot? previous)
            && previous.Names.Count > 0)
        {
            _servers[serverId] = previous with
            {
                Succeeded = false,
                LastFailureAt = now
            };
            return;
        }

        _servers[serverId] = new ServerPppSessionsSnapshot(
            serverId,
            [],
            now,
            Succeeded: false,
            LastFailureAt: now);
    }

    public bool TryGetServerSnapshot(int serverId, out ServerPppSessionsSnapshot? snapshot) =>
        _servers.TryGetValue(serverId, out snapshot);

    public IReadOnlyDictionary<int, IReadOnlyCollection<string>> GetUsableSessionNames(IEnumerable<int> serverIds)
    {
        Dictionary<int, IReadOnlyCollection<string>> result = [];
        foreach (int serverId in serverIds.Distinct())
        {
            if (_servers.TryGetValue(serverId, out ServerPppSessionsSnapshot? snapshot))
            {
                result[serverId] = snapshot.Names.ToHashSet(StringComparer.OrdinalIgnoreCase);
            }
        }

        return result;
    }

    public void SetCompanySnapshot(CompanyLiveConnectionSnapshot snapshot)
    {
        _companies[snapshot.CompanyScopeId] = snapshot with
        {
            ConnectedClientIds = [.. snapshot.ConnectedClientIds]
        };
    }

    public bool TryGetCompanySnapshot(int companyScopeId, out CompanyLiveConnectionSnapshot? snapshot)
    {
        if (!_companies.TryGetValue(companyScopeId, out snapshot) || snapshot == null)
        {
            snapshot = null;
            return false;
        }

        snapshot = snapshot with
        {
            ConnectedClientIds = [.. snapshot.ConnectedClientIds]
        };
        return true;
    }

    public Task<bool> TryEnterPollAsync(TimeSpan wait, CancellationToken ct = default) =>
        _pollLock.WaitAsync(wait, ct);

    public void ExitPoll()
    {
        try
        {
            _pollLock.Release();
        }
        catch (SemaphoreFullException)
        {
            // تجاهل تحرير زائد
        }
    }

    public void Dispose() => _pollLock.Dispose();
}
