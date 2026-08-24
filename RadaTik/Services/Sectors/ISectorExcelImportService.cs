using RadaTik.Models;

namespace RadaTik.Services.Sectors;

public sealed class SectorExcelImportParseResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int TotalRows { get; init; }
    public int ImportableCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public List<Sector> SectorsToAdd { get; init; } = [];
    public List<string> Details { get; init; } = [];

    public static SectorExcelImportParseResult Fail(string message) =>
        new() { Success = false, Message = message };

    public static SectorExcelImportParseResult FromRows(
        string message,
        int totalRows,
        int skipped,
        int failed,
        List<Sector> sectors,
        List<string> details) =>
        new()
        {
            Success = true,
            Message = message,
            TotalRows = totalRows,
            ImportableCount = sectors.Count,
            SkippedCount = skipped,
            FailedCount = failed,
            SectorsToAdd = sectors,
            Details = details
        };
}

public sealed class SectorExcelImportResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int AddedCount { get; init; }
    public int SkippedCount { get; init; }
    public int FailedCount { get; init; }
    public List<string> Details { get; init; } = [];

    public static SectorExcelImportResult Fail(string message, List<string>? details = null) =>
        new()
        {
            Success = false,
            Message = message,
            Details = details ?? []
        };

    public static SectorExcelImportResult Ok(
        string message,
        int added,
        int skipped,
        int failed,
        List<string> details) =>
        new()
        {
            Success = added > 0,
            Message = message,
            AddedCount = added,
            SkippedCount = skipped,
            FailedCount = failed,
            Details = details
        };
}

public interface ISectorExcelImportService
{
    byte[] BuildTemplateWorkbook();

    Task<SectorExcelImportParseResult> ParseAsync(
        Stream fileStream,
        string fileName,
        int networkId,
        CancellationToken ct = default);

    Task<SectorExcelImportResult> CommitAsync(
        IReadOnlyList<Sector> sectors,
        CancellationToken ct = default);
}
