namespace RadaTik.Services.Clients;

public sealed class SubscriberFaultEvidenceDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsAlert { get; init; }
}

public sealed class SubscriberFaultDiagnosisDto
{
    public bool Success { get; init; }
    public string Status { get; init; } = "Ok";
    public string? Message { get; init; }
    public string Cause { get; init; } = string.Empty;
    public string CauseLabel { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string ConfidenceLabel { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public string? SuggestedMaintenanceType { get; init; }
    public string? SuggestedMaintenanceLabel { get; init; }
    public IReadOnlyList<SubscriberFaultEvidenceDto> Evidence { get; init; } = [];
}

public interface ISubscriberFaultDiagnosisService
{
    Task<SubscriberFaultDiagnosisDto> DiagnoseAsync(
        int clientId,
        int selectedNetworkId,
        CancellationToken cancellationToken = default);
}
