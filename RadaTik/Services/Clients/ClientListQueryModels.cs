using RadaTik.Models;

namespace RadaTik.Services.Clients;

public enum ClientListAccessOutcome
{
    Ok,
    RequiresLogin,
    Forbidden,
    RequiresNetworkSelection,
    NotFound
}

public sealed class ClientIndexPageModel
{
    public ClientListAccessOutcome Access { get; init; }
    public List<Client> Clients { get; init; } = [];
    public Dictionary<int, string> DbAccountMap { get; init; } = new();
    public HashSet<int> PendingClientIds { get; init; } = [];
    public HashSet<int> ConnectedClientIds { get; init; } = [];
    /// <summary>true إذا كانت حالة الاتصال جاهزة من التخزين المؤقت (بدون انتظار MikroTik).</summary>
    public bool ConnectionsReady { get; init; }
    public List<Network> AvailableNetworks { get; init; } = [];
    public int? CurrentNetworkId { get; init; }
}

public sealed class ClientDetailsPageModel
{
    public ClientListAccessOutcome Access { get; init; }
    public Client? Client { get; init; }
    public bool IsPendingClientApproval { get; init; }
    public string? RenewalBlockedMessage { get; init; }
    public Client? MikroTikInfo { get; init; }
    public string? MikroTikError { get; init; }
    public bool IsClientView { get; init; }
    public bool IsClientOnly { get; init; }
    public List<ClientTopUpTransaction> RecentTopUps { get; init; } = [];
}
