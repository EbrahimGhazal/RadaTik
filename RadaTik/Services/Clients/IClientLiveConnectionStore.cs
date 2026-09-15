namespace RadaTik.Services.Clients;

public interface IClientLiveConnectionStore
{
    void SetServerResult(int serverId, IReadOnlyList<string> names, bool succeeded);

    bool TryGetServerSnapshot(int serverId, out ServerPppSessionsSnapshot? snapshot);

    IReadOnlyDictionary<int, IReadOnlyCollection<string>> GetUsableSessionNames(IEnumerable<int> serverIds);

    void SetCompanySnapshot(CompanyLiveConnectionSnapshot snapshot);

    bool TryGetCompanySnapshot(int companyScopeId, out CompanyLiveConnectionSnapshot? snapshot);

    Task<bool> TryEnterPollAsync(TimeSpan wait, CancellationToken ct = default);

    void ExitPoll();
}
