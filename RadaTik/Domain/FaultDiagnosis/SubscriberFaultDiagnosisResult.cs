using RadaTik.Models;

namespace RadaTik.Domain.FaultDiagnosis;

public sealed class SubscriberFaultEvidence
{
    public string Code { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public bool IsAlert { get; init; }
}

public sealed class SubscriberFaultDiagnosisResult
{
    public SubscriberFaultComponent Cause { get; init; } = SubscriberFaultComponent.Unknown;
    public SubscriberFaultConfidence Confidence { get; init; } = SubscriberFaultConfidence.Low;
    public string CauseLabel { get; init; } = string.Empty;
    public string ConfidenceLabel { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string SuggestedAction { get; init; } = string.Empty;
    public MaintenanceType? SuggestedMaintenanceType { get; init; }
    public IReadOnlyList<SubscriberFaultEvidence> Evidence { get; init; } = [];
}
