using RadaTik.Models;

namespace RadaTik.Services.Clients;

public enum BulkTopUpMode
{
    Fixed = 0,
    PercentOfPackage = 1
}

public sealed class ClientWalletTopUpCommand
{
    public required int ClientId { get; init; }
    public required decimal Amount { get; init; }
    public required string ActorUserId { get; init; }
    public required ClientTopUpSource SourceType { get; init; }
    public int? ActorNetworkId { get; init; }
    public string? Notes { get; init; }
    public string? ActorDisplayName { get; init; }
}

public sealed class ClientWalletTopUpOutcome
{
    public bool IsSuccess { get; init; }
    public bool NotFound { get; init; }
    public string? ErrorMessage { get; init; }
    public string? SuccessMessage { get; init; }

    public static ClientWalletTopUpOutcome Success(string message) =>
        new() { IsSuccess = true, SuccessMessage = message };

    public static ClientWalletTopUpOutcome Fail(string message) =>
        new() { ErrorMessage = message };

    public static ClientWalletTopUpOutcome NotFoundClient() =>
        new() { NotFound = true };
}

public sealed class BulkClientWalletTopUpCommand
{
    public required int NetworkId { get; init; }
    public required bool ApplyToAll { get; init; }
    public IReadOnlyList<int>? ClientIds { get; init; }
    public required BulkTopUpMode Mode { get; init; }
    /// <summary>مبلغ ثابت (ل.س) أو نسبة مئوية من سعر الباقة.</summary>
    public required decimal Value { get; init; }
    public required string ActorUserId { get; init; }
    public required ClientTopUpSource SourceType { get; init; }
    public int? ActorNetworkId { get; init; }
    public string? Notes { get; init; }
    public string? ActorDisplayName { get; init; }
}

public sealed class BulkClientWalletTopUpOutcome
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RequestedCount { get; init; }
    public int SucceededCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public decimal TotalCredited { get; init; }
    public List<string> Errors { get; init; } = [];

    public static BulkClientWalletTopUpOutcome Fail(string message) =>
        new() { IsSuccess = false, Message = message };

    public static BulkClientWalletTopUpOutcome Ok(
        string message,
        int requested,
        int succeeded,
        int skipped,
        int failed,
        decimal totalCredited,
        List<string> errors) =>
        new()
        {
            IsSuccess = succeeded > 0,
            Message = message,
            RequestedCount = requested,
            SucceededCount = succeeded,
            SkippedCount = skipped,
            FailedCount = failed,
            TotalCredited = totalCredited,
            Errors = errors
        };
}
