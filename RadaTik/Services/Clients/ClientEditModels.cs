using RadaTik.Models;

namespace RadaTik.Services.Clients;

public enum ClientEditStatus
{
    Success,
    EmployeePendingApproval,
    Failed,
    NotFound
}

public sealed class ClientEditRequest
{
    public required int ClientId { get; init; }
    public required Client SubmittedClient { get; init; }
    public string? DbUserName { get; init; }
    public string? DbPassword { get; init; }
    public required int NetworkId { get; init; }
    public required string ActorUserId { get; init; }
    public required bool IsEmployee { get; init; }
}

public sealed class ClientEditOutcome
{
    public ClientEditStatus Status { get; init; }
    public string? Message { get; init; }

    public static ClientEditOutcome Success(string message) =>
        new() { Status = ClientEditStatus.Success, Message = message };

    public static ClientEditOutcome EmployeePending(string message) =>
        new() { Status = ClientEditStatus.EmployeePendingApproval, Message = message };

    public static ClientEditOutcome Failed(string message) =>
        new() { Status = ClientEditStatus.Failed, Message = message };

    public static ClientEditOutcome NotFound() =>
        new() { Status = ClientEditStatus.NotFound };
}
