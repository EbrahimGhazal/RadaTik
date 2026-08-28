namespace RadaTik.Services.MikroTik;

public sealed class MikroTikPingHopResult
{
    public string Address { get; init; } = string.Empty;
    public bool Attempted { get; init; }
    public bool Reached { get; init; }
    public string? StatusMessage { get; init; }
}

public interface IMikroTikProbeService
{
    /// <summary>يفحص عناوين IPv4 عبر <c>/ping</c> من سيرفر MikroTik باتصال واحد.</summary>
    Task<IReadOnlyDictionary<string, MikroTikPingHopResult>> PingManyAsync(
        int serverId,
        IReadOnlyList<string> addresses,
        CancellationToken cancellationToken = default);
}
