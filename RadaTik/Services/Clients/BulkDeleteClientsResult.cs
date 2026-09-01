namespace RadaTik.Services.Clients;

public sealed class BulkDeleteClientsResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int RequestedCount { get; init; }
    public int DeletedCount { get; init; }
    public int FailedCount { get; init; }
    public int NotFoundCount { get; init; }
    public int MikroTikWarningCount { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];

    public static BulkDeleteClientsResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static BulkDeleteClientsResult Ok(
        int requested,
        int deleted,
        int failed,
        int notFound,
        int mikroTikWarnings,
        string message,
        IReadOnlyList<string>? errors = null) =>
        new()
        {
            Success = deleted > 0,
            RequestedCount = requested,
            DeletedCount = deleted,
            FailedCount = failed,
            NotFoundCount = notFound,
            MikroTikWarningCount = mikroTikWarnings,
            Message = message,
            Errors = errors ?? []
        };
}
