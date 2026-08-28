using RadaTik.Domain.FaultDiagnosis;
using RadaTik.Models;

namespace RadaTik.Services.Clients;

public sealed class SubscriberFaultEvidenceDto
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsAlert { get; init; }
}

public sealed class SubscriberFaultHopDto
{
    public string Name { get; init; } = string.Empty;
    public string? Address { get; init; }
    public string Status { get; init; } = "لم يُفحص";
}

public sealed class SubscriberFaultDiagnosisDto
{
    public bool Success { get; init; }
    public string Status { get; init; } = "Ok";
    public string? Message { get; init; }
    public long? DiagnosisId { get; init; }
    public int? ClientId { get; init; }
    public int? MaintenanceRequestId { get; init; }
    public string Cause { get; init; } = string.Empty;
    public string CauseLabel { get; init; } = string.Empty;
    public string Confidence { get; init; } = string.Empty;
    public string ConfidenceLabel { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public string? SuggestedMaintenanceType { get; init; }
    public string? SuggestedMaintenanceLabel { get; init; }
    public bool CanCreateMaintenance { get; init; }
    public IReadOnlyList<SubscriberFaultHopDto> Hops { get; init; } = [];
    public IReadOnlyList<SubscriberFaultEvidenceDto> Evidence { get; init; } = [];
    public string? ConfirmedCause { get; init; }
    public string? ConfirmedCauseLabel { get; init; }
    public string? ConfirmedMaintenanceLabel { get; init; }
    public bool? SuggestionMatched { get; init; }
    public DateTime CreatedAt { get; init; }
}

public interface ISubscriberFaultDiagnosisService
{
    Task<SubscriberFaultDiagnosisDto> DiagnoseAsync(
        int clientId,
        int selectedNetworkId,
        SubscriberFaultLedAnswers? led = null,
        string? createdByUserId = null,
        CancellationToken cancellationToken = default);

    Task<SubscriberFaultDiagnosisDto> LinkToMaintenanceRequestAsync(
        long diagnosisId,
        int maintenanceRequestId,
        CancellationToken cancellationToken = default);

    Task ConfirmFromMaintenanceAsync(
        int maintenanceRequestId,
        IReadOnlyList<MaintenanceType> selectedTypes,
        string? confirmedByUserId,
        CancellationToken cancellationToken = default);

    Task<SubscriberFaultDiagnosisDto?> GetForMaintenanceRequestAsync(
        int maintenanceRequestId,
        CancellationToken cancellationToken = default);

    Task<SubscriberFaultDiagnosisDto?> GetByIdAsync(
        long diagnosisId,
        CancellationToken cancellationToken = default);
}
