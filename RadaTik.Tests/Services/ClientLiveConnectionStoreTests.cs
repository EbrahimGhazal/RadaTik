using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientLiveConnectionStoreTests
{
    [Fact]
    public void SetServerResult_KeepsLastGoodNamesOnFailure()
    {
        ClientLiveConnectionStore store = new();
        store.SetServerResult(4, ["user-a", "user-b"], succeeded: true);
        store.SetServerResult(4, [], succeeded: false);

        Assert.True(store.TryGetServerSnapshot(4, out ServerPppSessionsSnapshot? snapshot));
        Assert.False(snapshot!.Succeeded);
        Assert.Equal(new[] { "user-a", "user-b" }, snapshot.Names);
        Assert.NotNull(snapshot.LastFailureAt);

        IReadOnlyDictionary<int, IReadOnlyCollection<string>> usable = store.GetUsableSessionNames([4, 9]);
        Assert.True(usable.ContainsKey(4));
        Assert.False(usable.ContainsKey(9));
        Assert.Contains("user-a", usable[4]);
    }

    [Fact]
    public void CompanySnapshot_IsCopiedOnRead()
    {
        ClientLiveConnectionStore store = new();
        store.SetCompanySnapshot(new CompanyLiveConnectionSnapshot(
            10,
            [1, 2],
            DateTimeOffset.UtcNow,
            Ready: true));

        Assert.True(store.TryGetCompanySnapshot(10, out CompanyLiveConnectionSnapshot? first));
        first!.ConnectedClientIds.Add(99);

        Assert.True(store.TryGetCompanySnapshot(10, out CompanyLiveConnectionSnapshot? second));
        Assert.DoesNotContain(99, second!.ConnectedClientIds);
        Assert.True(second.Ready);
    }
}
