namespace RadaTik.Services.MikroTik;

public sealed class BulkAddPppoeUsersResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int AddedCount { get; init; }
    public int SkippedExistingCount { get; init; }
    public int SkippedInvalidCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<int> PlacedClientIds { get; init; } = [];
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static BulkAddPppoeUsersResult Fail(string message) =>
        new() { Success = false, Message = message };
}

public sealed class BulkDeletePppoeUsersResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int DeletedCount { get; init; }
    public int NotFoundCount { get; init; }
    public int FailedCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static BulkDeletePppoeUsersResult Fail(string message) =>
        new() { Success = false, Message = message };
}
