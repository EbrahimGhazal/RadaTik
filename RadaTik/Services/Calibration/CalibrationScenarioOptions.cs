using System.Text.Json;
using RadaTik.Models;

namespace RadaTik.Services.Calibration;

public static class CalibrationOptionValues
{
    public const string CrewSolo = "solo";
    public const string CrewDual = "dual";

    public const string AimSignal = "signal";
    public const string AimCompass = "compass";
    public const string AimGeometry = "geometry";
    public const string AimPro = "pro";

    public const string AuthQr = "qr";
    public const string AuthLogin = "login";

    public const string DisplayCurrent = "current";
    public const string DisplayPeak = "peak";
    public const string DisplaySteer = "steer";

    public const string SuccessPeak = "peak";
    public const string SuccessHold = "hold";
    public const string SuccessMinDbm = "mindbm";

    public static string NormalizeCrew(string? v) =>
        string.Equals(v, CrewDual, StringComparison.OrdinalIgnoreCase) ? CrewDual : CrewSolo;

    public static string NormalizeAim(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        AimCompass or "compass-then-signal" => AimCompass,
        AimGeometry or "az" or "azimuth" => AimGeometry,
        AimPro or "professional" => AimPro,
        _ => AimSignal
    };

    public static string NormalizeAuth(string? v) =>
        string.Equals(v, AuthLogin, StringComparison.OrdinalIgnoreCase) ? AuthLogin : AuthQr;

    public static string NormalizeDisplay(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        DisplayCurrent or "one" => DisplayCurrent,
        DisplaySteer or "arrows" => DisplaySteer,
        _ => DisplayPeak
    };

    public static string NormalizeSuccess(string? v) => v?.Trim().ToLowerInvariant() switch
    {
        SuccessHold or "lock" => SuccessHold,
        SuccessMinDbm or "min" => SuccessMinDbm,
        _ => SuccessPeak
    };

    public static string AimLabel(string aim) => NormalizeAim(aim) switch
    {
        AimCompass => "بوصلة ثم إشارة",
        AimGeometry => "سمت/ميل هندسي",
        AimPro => "احترافي (أفقي→عمودي)",
        _ => "قمة إشارة"
    };

    public static string CrewLabel(string crew) => NormalizeCrew(crew) == CrewDual ? "فنيان" : "فني واحد";

    public static string SuccessLabel(string mode, int? minDbm, int holdSeconds) => NormalizeSuccess(mode) switch
    {
        SuccessHold => $"قفل بعد ثبات {Math.Max(1, holdSeconds)} ث",
        SuccessMinDbm => minDbm is int m ? $"حد أدنى {m} dBm" : "حد أدنى للإشارة",
        _ => "أعلى قمة ظهرت"
    };
}

/// <summary>لقطة سيناريو تُثبَّت داخل الجلسة حتى لا تتأثر بتعديل لاحق.</summary>
public sealed class CalibrationScenarioSnapshot
{
    public int? ScenarioId { get; init; }
    public string Name { get; init; } = "تركيب يومي";
    public string CrewMode { get; init; } = CalibrationOptionValues.CrewSolo;
    public string AimMode { get; init; } = CalibrationOptionValues.AimSignal;
    public string AuthMode { get; init; } = CalibrationOptionValues.AuthQr;
    public string DisplayMode { get; init; } = CalibrationOptionValues.DisplayPeak;
    public string SuccessMode { get; init; } = CalibrationOptionValues.SuccessPeak;
    public int? MinSignalDbm { get; init; }
    public int PeakHoldSeconds { get; init; } = 3;
    public bool RequireIpOrMac { get; init; } = true;
    public bool ShowSnrCcq { get; init; }
    public bool ShowLosHint { get; init; } = true;
    public bool ShowGeometryTargets { get; init; }

    public bool IsDual => CalibrationOptionValues.NormalizeCrew(CrewMode) == CalibrationOptionValues.CrewDual;
    public bool NeedsRadio => CalibrationOptionValues.NormalizeAim(AimMode) != CalibrationOptionValues.AimGeometry;
    public bool NeedsCompass =>
        CalibrationOptionValues.NormalizeAim(AimMode) is CalibrationOptionValues.AimCompass
            or CalibrationOptionValues.AimGeometry
            or CalibrationOptionValues.AimPro
        || CalibrationOptionValues.NormalizeDisplay(DisplayMode) == CalibrationOptionValues.DisplaySteer;

    public static CalibrationScenarioSnapshot FromEntity(CalibrationScenario s) => new()
    {
        ScenarioId = s.Id,
        Name = s.Name,
        CrewMode = CalibrationOptionValues.NormalizeCrew(s.CrewMode),
        AimMode = CalibrationOptionValues.NormalizeAim(s.AimMode),
        AuthMode = CalibrationOptionValues.NormalizeAuth(s.AuthMode),
        DisplayMode = CalibrationOptionValues.NormalizeDisplay(s.DisplayMode),
        SuccessMode = CalibrationOptionValues.NormalizeSuccess(s.SuccessMode),
        MinSignalDbm = s.MinSignalDbm,
        PeakHoldSeconds = s.PeakHoldSeconds < 1 ? 3 : s.PeakHoldSeconds,
        RequireIpOrMac = s.RequireIpOrMac,
        ShowSnrCcq = s.ShowSnrCcq,
        ShowLosHint = s.ShowLosHint,
        ShowGeometryTargets = s.ShowGeometryTargets
            || CalibrationOptionValues.NormalizeAim(s.AimMode) is CalibrationOptionValues.AimGeometry
                or CalibrationOptionValues.AimCompass
                or CalibrationOptionValues.AimPro
    };

    public static CalibrationScenarioSnapshot DailyDefault() => new()
    {
        Name = "تركيب يومي",
        CrewMode = CalibrationOptionValues.CrewSolo,
        AimMode = CalibrationOptionValues.AimSignal,
        AuthMode = CalibrationOptionValues.AuthQr,
        DisplayMode = CalibrationOptionValues.DisplayPeak,
        SuccessMode = CalibrationOptionValues.SuccessPeak,
        PeakHoldSeconds = 3,
        RequireIpOrMac = true,
        ShowLosHint = true
    };

    public string ToJson() => JsonSerializer.Serialize(this);

    public static CalibrationScenarioSnapshot FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DailyDefault();
        }

        try
        {
            return JsonSerializer.Deserialize<CalibrationScenarioSnapshot>(json) ?? DailyDefault();
        }
        catch
        {
            return DailyDefault();
        }
    }
}

public static class CalibrationScenarioTemplates
{
    public static IReadOnlyList<CalibrationScenario> BuildDefaults(int networkId)
    {
        DateTime now = DateTime.UtcNow;
        return
        [
            new CalibrationScenario
            {
                NetworkId = networkId,
                Name = "تركيب يومي",
                Description = "فني واحد عند المستقبل. حرّك حتى أعلى رقم ثم اربط.",
                IsDefault = true,
                CrewMode = CalibrationOptionValues.CrewSolo,
                AimMode = CalibrationOptionValues.AimSignal,
                AuthMode = CalibrationOptionValues.AuthQr,
                DisplayMode = CalibrationOptionValues.DisplayPeak,
                SuccessMode = CalibrationOptionValues.SuccessPeak,
                RequireIpOrMac = true,
                ShowLosHint = true,
                SortOrder = 1,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new CalibrationScenario
            {
                NetworkId = networkId,
                Name = "فنيان",
                Description = "مرسل + مستقبل معاً مع قفل قمة بعد ثبات قصير.",
                CrewMode = CalibrationOptionValues.CrewDual,
                AimMode = CalibrationOptionValues.AimSignal,
                AuthMode = CalibrationOptionValues.AuthQr,
                DisplayMode = CalibrationOptionValues.DisplayPeak,
                SuccessMode = CalibrationOptionValues.SuccessHold,
                PeakHoldSeconds = 3,
                RequireIpOrMac = true,
                ShowSnrCcq = true,
                ShowLosHint = true,
                SortOrder = 2,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new CalibrationScenario
            {
                NetworkId = networkId,
                Name = "احترافي دقيق",
                Description = "مسح أفقي ثم عمودي، SNR/CCQ، وقفل قمة مستقر.",
                CrewMode = CalibrationOptionValues.CrewDual,
                AimMode = CalibrationOptionValues.AimPro,
                AuthMode = CalibrationOptionValues.AuthQr,
                DisplayMode = CalibrationOptionValues.DisplayPeak,
                SuccessMode = CalibrationOptionValues.SuccessHold,
                PeakHoldSeconds = 3,
                RequireIpOrMac = true,
                ShowSnrCcq = true,
                ShowLosHint = true,
                ShowGeometryTargets = true,
                SortOrder = 3,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new CalibrationScenario
            {
                NetworkId = networkId,
                Name = "تقريب بوصلة",
                Description = "أسهم بوصلة للتقريب ثم قفل نهائي بقمة الإشارة.",
                CrewMode = CalibrationOptionValues.CrewSolo,
                AimMode = CalibrationOptionValues.AimCompass,
                AuthMode = CalibrationOptionValues.AuthQr,
                DisplayMode = CalibrationOptionValues.DisplaySteer,
                SuccessMode = CalibrationOptionValues.SuccessPeak,
                RequireIpOrMac = true,
                ShowGeometryTargets = true,
                ShowLosHint = true,
                SortOrder = 4,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            },
            new CalibrationScenario
            {
                NetworkId = networkId,
                Name = "سمت هندسي فقط",
                Description = "بدون إشارة حية: سمت وميل محسوبان من المواقع.",
                CrewMode = CalibrationOptionValues.CrewSolo,
                AimMode = CalibrationOptionValues.AimGeometry,
                AuthMode = CalibrationOptionValues.AuthQr,
                DisplayMode = CalibrationOptionValues.DisplaySteer,
                SuccessMode = CalibrationOptionValues.SuccessPeak,
                RequireIpOrMac = false,
                ShowGeometryTargets = true,
                ShowLosHint = true,
                SortOrder = 5,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            }
        ];
    }
}
