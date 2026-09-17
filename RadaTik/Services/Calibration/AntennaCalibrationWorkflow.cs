namespace RadaTik.Services.Calibration;

/// <summary>أساليب معايرة الجلسة: سريع / إشارة / احترافي.</summary>
public static class AntennaCalibrationWorkflow
{
    public const string Quick = "quick";
    public const string Signal = "signal";
    public const string Pro = "pro";

    public static string Normalize(string? raw)
    {
        if (string.Equals(raw, Quick, StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "compass", StringComparison.OrdinalIgnoreCase))
        {
            return Quick;
        }

        if (string.Equals(raw, Pro, StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "professional", StringComparison.OrdinalIgnoreCase))
        {
            return Pro;
        }

        return Signal;
    }

    public static bool IsPro(string? workflow) => Normalize(workflow) == Pro;

    public static bool IsQuick(string? workflow) => Normalize(workflow) == Quick;

    public static string DisplayName(string? workflow) => Normalize(workflow) switch
    {
        Quick => "تقريبي (بوصلة)",
        Pro => "احترافي (مراحل + قفل قمة)",
        _ => "قمة إشارة"
    };

    public static string DefaultFieldMode(string? workflow) => IsQuick(workflow) ? "compass" : "signal";
}
