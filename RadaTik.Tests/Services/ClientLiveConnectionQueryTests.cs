using Xunit;

namespace RadaTik.Tests.Services;

public sealed class ClientLiveConnectionQueryTests
{
    [Fact]
    public void ClientListQuery_ReadsCachedLiveConnectionSnapshot()
    {
        string query = File.ReadAllText(FindFile("RadaTik", "Services", "Clients", "ClientListQueryService.cs"));
        Assert.Contains("GetLiveConnectionStatusAsync", query);
        Assert.Contains("IClientLiveConnectionStore", query);
        Assert.DoesNotContain("GetActivePPPoEUsers(serverId)", query);
        Assert.DoesNotContain("GetActivePppSessionNamesByServerAsync", query);

        string refresh = File.ReadAllText(FindFile("RadaTik", "Services", "Clients", "ClientLiveConnectionRefreshService.cs"));
        Assert.Contains("QueryActivePppSessionNamesByServerAsync", refresh);
        Assert.Contains("s.IsActive", refresh);

        string service = File.ReadAllText(FindFile("RadaTik", "Services", "MikroTik", "MikroTikUserService.cs"));
        Assert.Contains("QueryActivePppSessionNamesByServerAsync", service);
        Assert.Contains("CreateQuickReadConnection", service);
        Assert.Contains("Task.WhenAll", service);
        Assert.Contains("PrintList(connection, \"/ppp/active/print\", \"name\")", service);

        string contract = File.ReadAllText(FindFile("RadaTik", "Controllers", "ClientsController.ListAndContract.cs"));
        Assert.Contains("ConnectionStatusJson", contract);
        Assert.Contains("GetLiveConnectionStatusAsync", contract);
        Assert.Contains("ready = status.Ready", contract);

        string hosted = File.ReadAllText(FindFile("RadaTik", "ServiceRegistrationExtensions.cs"));
        Assert.Contains("ClientLiveConnectionBackgroundService", hosted);
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
