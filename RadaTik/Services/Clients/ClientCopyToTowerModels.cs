namespace RadaTik.Services.Clients;

public sealed class ClientCopyTargetServerItem
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? Host { get; init; }
}

public sealed class BulkCopyAccountsToServerResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RequestedCount { get; init; }
    public int AddedCount { get; init; }
    public int SkippedExistingCount { get; init; }
    public int SkippedInvalidCount { get; init; }
    public int FailedCount { get; init; }
    public int ReassignedCount { get; init; }
    public int RemovedFromOldCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static BulkCopyAccountsToServerResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static BulkCopyAccountsToServerResult Ok(
        int requested,
        int added,
        int skippedExisting,
        int skippedInvalid,
        int failed,
        int reassigned,
        int removedFromOld,
        string message,
        IReadOnlyList<string>? errors = null) =>
        new()
        {
            Success = true,
            RequestedCount = requested,
            AddedCount = added,
            SkippedExistingCount = skippedExisting,
            SkippedInvalidCount = skippedInvalid,
            FailedCount = failed,
            ReassignedCount = reassigned,
            RemovedFromOldCount = removedFromOld,
            Message = message,
            Errors = errors ?? []
        };
}
