using RadaTik.Models;
using RadaTik.Services.Clients;
using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientLiveConnectionMatcherTests
{
    [Fact]
    public void Match_IncludesDisabledClientOnSameServer()
    {
        List<Client> clients =
        [
            new() { Id = 1, UserName = "user-a", MikroTikServerId = 10, IsActive = false },
            new() { Id = 2, UserName = "user-b", MikroTikServerId = 10, IsActive = true }
        ];
        Dictionary<int, IReadOnlyCollection<string>> sessions = new()
        {
            [10] = ["user-a", "user-b"]
        };

        HashSet<int> ids = ClientLiveConnectionMatcher.Match(clients, sessions);

        Assert.Equal(new[] { 1, 2 }, ids.OrderBy(id => id).ToArray());
    }

    [Fact]
    public void Match_PrefersSameServerWhenUsernameExistsOnMultipleTowers()
    {
        List<Client> clients =
        [
            new() { Id = 1, UserName = "same-user", MikroTikServerId = 10, IsActive = true },
            new() { Id = 2, UserName = "same-user", MikroTikServerId = 20, IsActive = true }
        ];
        Dictionary<int, IReadOnlyCollection<string>> sessions = new()
        {
            [20] = ["same-user"]
        };

        HashSet<int> ids = ClientLiveConnectionMatcher.Match(clients, sessions);

        Assert.Single(ids);
        Assert.Contains(2, ids);
    }

    [Fact]
    public void Match_FallsBackToUsernameWhenSessionServerHasNoLocalClient()
    {
        List<Client> clients =
        [
            new() { Id = 7, UserName = "copied-user", MikroTikServerId = 10, IsActive = true }
        ];
        Dictionary<int, IReadOnlyCollection<string>> sessions = new()
        {
            [20] = ["copied-user"]
        };

        HashSet<int> ids = ClientLiveConnectionMatcher.Match(clients, sessions);

        Assert.Single(ids);
        Assert.Contains(7, ids);
    }

    [Fact]
    public void Match_NormalizesQuotedAndSpacedUserNames()
    {
        List<Client> clients =
        [
            new() { Id = 3, UserName = "  quoted  ", MikroTikServerId = 5, IsActive = true }
        ];
        Dictionary<int, IReadOnlyCollection<string>> sessions = new()
        {
            [5] = ["\"quoted\""]
        };

        HashSet<int> ids = ClientLiveConnectionMatcher.Match(clients, sessions);

        Assert.Single(ids);
        Assert.Contains(3, ids);
    }
}
