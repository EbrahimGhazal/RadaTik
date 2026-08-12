using RadaTik.Models;

namespace RadaTik.Services.Clients;

public enum ClientCreateStatus
{
    Success,
    EmployeePendingApproval,
    ValidationError,
    Failed
}

public sealed class ClientCreateRequest
{
    public required Client Client { get; init; }
    public string? DbUserName { get; init; }
    public string? DbPassword { get; init; }
    public required int NetworkId { get; init; }
    public required string ActorUserId { get; init; }
    public required bool IsEmployee { get; init; }
}

public sealed class ClientValidationResult
{
    public bool IsValid => Errors.Count == 0;
    public Dictionary<string, string> Errors { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static ClientValidationResult Ok() => new();

    public void Add(string field, string message) => Errors[field] = message;
}

public sealed class ClientCreateOutcome
{
    public ClientCreateStatus Status { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, string>? FieldErrors { get; init; }

    public static ClientCreateOutcome Success(string message) =>
        new() { Status = ClientCreateStatus.Success, Message = message };

    public static ClientCreateOutcome EmployeePending(string message) =>
        new() { Status = ClientCreateStatus.EmployeePendingApproval, Message = message };

    public static ClientCreateOutcome Validation(Dictionary<string, string> errors) =>
        new() { Status = ClientCreateStatus.ValidationError, FieldErrors = errors };

    public static ClientCreateOutcome Failed(string message) =>
        new() { Status = ClientCreateStatus.Failed, Message = message };
}
