using RadaTik.Models;
using RadaTik.Services.Calibration;

namespace RadaTik.ViewModels.Calibration;

public sealed class CalibrationIndexViewModel
{
    public List<CalibrationSectorOption> Sectors { get; set; } = [];
    public List<CalibrationScenario> Scenarios { get; set; } = [];
    public List<CalibrationSessionListItem> RecentSessions { get; set; } = [];
    public CalibrationSnapshot? Session { get; set; }
    public string? TransmitterJoinUrl { get; set; }
    public string? ReceiverJoinUrl { get; set; }
    public string? TransmitterQrDataUri { get; set; }
    public string? ReceiverQrDataUri { get; set; }
    public string? ErrorMessage { get; set; }
    public int? PrefillScenarioId { get; set; }
    public int? PrefillSectorId { get; set; }
    public int? PrefillReceiverId { get; set; }
}

public sealed record CalibrationSectorOption(int Id, string Name, double Lat, double Lon);

public sealed class CalibrationJoinViewModel
{
    public required string Code { get; init; }
    public required string Role { get; init; }
    public required bool IsTransmitter { get; init; }
    public required string Title { get; init; }
    public required string EndpointName { get; init; }
    public required string OtherName { get; init; }
    public required CalibrationScenarioSnapshot Scenario { get; init; }
    public required CalibrationSnapshot Snapshot { get; init; }
}

public sealed class CalibrationScenarioEditViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public string CrewMode { get; set; } = CalibrationOptionValues.CrewSolo;
    public string AimMode { get; set; } = CalibrationOptionValues.AimSignal;
    public string AuthMode { get; set; } = CalibrationOptionValues.AuthQr;
    public string DisplayMode { get; set; } = CalibrationOptionValues.DisplayPeak;
    public string SuccessMode { get; set; } = CalibrationOptionValues.SuccessPeak;
    public int? MinSignalDbm { get; set; }
    public int PeakHoldSeconds { get; set; } = 3;
    public bool RequireIpOrMac { get; set; } = true;
    public bool ShowSnrCcq { get; set; }
    public bool ShowLosHint { get; set; } = true;
    public bool ShowGeometryTargets { get; set; }
    public int SortOrder { get; set; }
}
