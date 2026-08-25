using RadaTik.Models;

namespace RadaTik.Services.Receivers;

public sealed class ReceiverExcelImportParseResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int ImportableCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public List<Receiver> ReceiversToAdd { get; init; } = [];
    public List<string> Details { get; init; } = [];

    public static ReceiverExcelImportParseResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static ReceiverExcelImportParseResult FromRows(
        string message,
        int totalRows,
        int skipped,
        int failed,
        List<Receiver> receivers,
        List<string> details) =>
        new()
        {
            Success = true,
            Message = message,
            TotalRows = totalRows,
            ImportableCount = receivers.Count,
            SkippedCount = skipped,
            FailedCount = failed,
            ReceiversToAdd = receivers,
            Details = details
        };
}

public sealed class ReceiverExcelImportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int AddedCount { get; init; }

    public static ReceiverExcelImportResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static ReceiverExcelImportResult Ok(string message, int added) =>
        new() { Success = added > 0, Message = message, AddedCount = added };
}

public interface IReceiverExcelImportService
{
    byte[] BuildTemplateWorkbook();

    Task<byte[]> BuildExportWorkbookAsync(int networkId, CancellationToken ct = default);

    Task<ReceiverExcelImportParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        int networkId,
        CancellationToken ct = default);

    Task<ReceiverExcelImportResult> CommitAsync(
        IReadOnlyList<Receiver> receivers,
        CancellationToken ct = default);
}
