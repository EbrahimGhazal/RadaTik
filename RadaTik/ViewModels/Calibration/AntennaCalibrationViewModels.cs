using RadaTik.Services.Calibration;

namespace RadaTik.ViewModels.Calibration;

public sealed class AntennaCalibrationIndexViewModel
{
    public List<AntennaCalibrationSectorOption> Sectors { get; set; } = [];
    public List<AntennaCalibrationSessionListItem> RecentSessions { get; set; } = [];
    public AntennaCalibrationSnapshot? Session { get; set; }
    public string? TransmitterJoinUrl { get; set; }
    public string? ReceiverJoinUrl { get; set; }
    public string? TransmitterQrDataUri { get; set; }
    public string? ReceiverQrDataUri { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed record AntennaCalibrationSectorOption(int Id, string Name, double Lat, double Lon);

public sealed class AntennaCalibrationJoinViewModel
{
    public required string Code { get; init; }
    public required string Role { get; init; }
    public required bool IsTransmitter { get; init; }
    public required string Title { get; init; }
    public required string EndpointName { get; init; }
    public required string OtherName { get; init; }
    public required AntennaCalibrationSnapshot Snapshot { get; init; }
}
