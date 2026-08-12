using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class ClientExpiredAccountsPageModel
{
    public required IReadOnlyList<Client> Accounts { get; init; }
    public int TotalExpired { get; init; }
    public int ActiveExpired { get; init; }
    public int DisabledExpired { get; init; }
}

public sealed class ClientExpiringSoonPageModel
{
    public required IReadOnlyList<Client> Accounts { get; init; }
    public int TotalExpiring { get; init; }
    public int ExpiringToday { get; init; }
    public int ExpiringTomorrow { get; init; }
    public int ExpiringIn2Days { get; init; }
    public int ExpiringIn3Days { get; init; }
}

public sealed class ClientRenewSubscriptionPageModel
{
    public int ClientId { get; init; }
    public string? ClientName { get; init; }
    public DateTime? CurrentExpirationDate { get; init; }
}

public sealed class BulkExpirationUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int UpdatedCount { get; init; }
    public int RequestedCount { get; init; }

    public static BulkExpirationUpdateResult Ok(int updated, int requested, string message) =>
        new() { Success = true, UpdatedCount = updated, RequestedCount = requested, Message = message };

    public static BulkExpirationUpdateResult Fail(string message) =>
        new() { Success = false, Message = message };
}
