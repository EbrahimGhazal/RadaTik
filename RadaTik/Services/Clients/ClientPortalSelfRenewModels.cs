namespace RadaTik.Services.Clients;

public enum ClientPortalSelfRenewStatus
{
    Success,
    NotFound,
    InvalidPrice,
    RenewalBlocked,
    InsufficientBalance,
    MissingNetwork,
    MissingActor,
    CommissionFailed,
    Error
}

public sealed class ClientPortalSelfRenewCommand
{
    public required int ClientId { get; init; }
    public required string ActorUserId { get; init; }
}

public sealed class ClientPortalSelfRenewOutcome
{
    public ClientPortalSelfRenewStatus Status { get; init; }
    public string? Message { get; init; }
    public bool RedirectToMaintenanceInvoices { get; init; }
    public bool RedirectToRenewSubscription { get; init; }

    public static ClientPortalSelfRenewOutcome Success(string message) =>
        new() { Status = ClientPortalSelfRenewStatus.Success, Message = message };

    public static ClientPortalSelfRenewOutcome Fail(
        ClientPortalSelfRenewStatus status,
        string message,
        bool maintenance = false,
        bool renewPage = true) =>
        new()
        {
            Status = status,
            Message = message,
            RedirectToMaintenanceInvoices = maintenance,
            RedirectToRenewSubscription = renewPage && !maintenance
        };
}
