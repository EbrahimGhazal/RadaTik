namespace RadTik.Services.Traffic;

public interface ITrafficMonitoringCoordinator
{
    void RegisterClient(string connectionId, (int networkId, int serverId) networkApiKey);

    void UnregisterClient(string connectionId, (int networkId, int serverId) networkApiKey);

    void UnregisterAllForConnection(string connectionId);

    IReadOnlyCollection<(int NetworkId, int ServerId)> GetActiveTargets();
}
