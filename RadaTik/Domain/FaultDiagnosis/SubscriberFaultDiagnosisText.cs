namespace RadaTik.Domain.FaultDiagnosis;

public static class SubscriberFaultDiagnosisText
{
    public static string AppendToDescription(string? description, string causeLabel, string summary, int maxLength = 1000)
    {
        string line = $"تشخيص تلقائي: {causeLabel} — {summary}";
        string combined = string.IsNullOrWhiteSpace(description)
            ? line
            : description.Trim() + Environment.NewLine + line;
        if (combined.Length <= maxLength)
        {
            return combined;
        }

        return combined[..maxLength];
    }
}
