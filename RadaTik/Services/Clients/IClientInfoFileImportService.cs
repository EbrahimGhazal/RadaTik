namespace RadaTik.Services.Clients;

public sealed class ClientInfoFileImportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int UpdatedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> Details { get; init; } = [];

    public static ClientInfoFileImportResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static ClientInfoFileImportResult Ok(
        string message,
        int totalRows,
        int updated,
        int skipped,
        int failed,
        List<string> details) =>
        new()
        {
            Success = updated > 0 || (failed == 0 && skipped >= 0 && totalRows >= 0),
            Message = message,
            TotalRows = totalRows,
            UpdatedCount = updated,
            SkippedCount = skipped,
            FailedCount = failed,
            Details = details
        };
}

public interface IClientInfoFileImportService
{
    Task<ClientInfoFileImportResult> ImportAsync(
        Stream fileStream,
        string fileName,
        int networkId,
        CancellationToken ct = default);

    byte[] BuildTemplateWorkbook();
}
