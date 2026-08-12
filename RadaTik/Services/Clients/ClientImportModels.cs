using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class ClientImportPageModel
{
    public required IReadOnlyDictionary<int, ImportUsersPreviewResult> PreviewByServer { get; init; }
    public required IReadOnlyDictionary<int, UsageImportChargeEstimate> ChargeByServer { get; init; }
    public decimal SubscriberUnitPrice { get; init; }
}

public sealed class ClientImportFromServerViewModel
{
    public required IReadOnlyList<MikroTikServer> Servers { get; init; }
    public required ClientImportPageModel ImportPage { get; init; }
}

public sealed class MikroTikServerUsersImportContext
{
    public required ImportUsersPreviewResult Preview { get; init; }
    public required UsageImportChargeEstimate Estimate { get; init; }
    public decimal SubscriberUnitPrice { get; init; }
}

public sealed class ClientImportOutcome
{
    public bool Success { get; init; }
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Warnings { get; init; }
    public string? FailedUsersJson { get; init; }
    public int DuplicateCount { get; init; }

    public static ClientImportOutcome Succeeded(
        string message,
        string? warnings = null,
        string? failedUsersJson = null,
        int duplicateCount = 0) =>
        new()
        {
            Success = true,
            SuccessMessage = message,
            Warnings = warnings,
            FailedUsersJson = failedUsersJson,
            DuplicateCount = duplicateCount
        };

    public static ClientImportOutcome Failed(string message) =>
        new() { Success = false, ErrorMessage = message };
}
