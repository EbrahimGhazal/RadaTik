namespace RadaTik.Services.Traffic;

public sealed class TrafficMonitoringCoordinator : ITrafficMonitoringCoordinator
{
    private readonly object _gate = new();
    private readonly Dictionary<(int NetworkId, int ServerId), int> _refByKey = new();
    private readonly Dictionary<string, HashSet<(int NetworkId, int ServerId)>> _keysByConnection = new();

    public void RegisterClient(string connectionId, (int networkId, int serverId) key)
    {
        lock (_gate)
        {
            if (!_keysByConnection.TryGetValue(connectionId, out var set))
            {
                set = new HashSet<(int NetworkId, int ServerId)>();
                _keysByConnection[connectionId] = set;
            }

            if (!set.Add(key))
            {
                return;
            }

            _refByKey.TryGetValue(key, out var n);
            _refByKey[key] = n + 1;
        }
    }

    public void UnregisterClient(string connectionId, (int networkId, int serverId) key)
    {
        lock (_gate)
        {
            if (!_keysByConnection.TryGetValue(connectionId, out var set))
            {
                return;
            }

            if (!set.Remove(key))
            {
                return;
            }

            if (set.Count == 0)
            {
                _keysByConnection.Remove(connectionId);
            }

            DecrementKey(key);
        }
    }

    public void UnregisterAllForConnection(string connectionId)
    {
        lock (_gate)
        {
            if (!_keysByConnection.TryGetValue(connectionId, out var set))
            {
                return;
            }

            _keysByConnection.Remove(connectionId);
            foreach (var key in set)
            {
                DecrementKey(key);
            }
        }
    }

    public IReadOnlyCollection<(int NetworkId, int ServerId)> GetActiveTargets()
    {
        lock (_gate)
        {
            return _refByKey.Where(kv => kv.Value > 0).Select(kv => kv.Key).ToArray();
        }
    }

    private void DecrementKey((int NetworkId, int ServerId) key)
    {
        if (!_refByKey.TryGetValue(key, out var n))
        {
            return;
        }

        n--;
        if (n <= 0)
        {
            _refByKey.Remove(key);
        }
        else
        {
            _refByKey[key] = n;
        }
    }
}
