using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientLiveConnectionQueryTests
{
    [Fact]
    public void ClientListQuery_UsesParallelPppActiveNames()
    {
        string query = File.ReadAllText(FindFile("RadaTik", "Services", "Clients", "ClientListQueryService.cs"));
        Assert.Contains("GetActivePppSessionNamesByServerAsync", query);
        Assert.Contains("/ppp/active", query);
        Assert.DoesNotContain("GetActivePPPoEUsers(serverId)", query);

        string service = File.ReadAllText(FindFile("RadaTik", "Services", "MikroTik", "MikroTikUserService.cs"));
        Assert.Contains("GetActivePppSessionNamesByServerAsync", service);
        Assert.Contains("Task.WhenAll", service);
        Assert.Contains("PrintList(connection, \"/ppp/active/print\", \"name\")", service);

        string contract = File.ReadAllText(FindFile("RadaTik", "Controllers", "ClientsController.ListAndContract.cs"));
        Assert.Contains("ConnectionStatusJson", contract);
        Assert.Contains("GetLiveConnectedClientIdsAsync", contract);
    }

    private static string FindFile(params string[] relativeParts)
    {
        string? dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            string candidate = Path.Combine(new[] { dir }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = Directory.GetParent(dir)?.FullName;
        }

        throw new FileNotFoundException(Path.Combine(relativeParts));
    }
}
