namespace RadaTik.Models;

/// <summary>سيناريو معايرة محفوظ لكل شبكة.</summary>
public sealed class CalibrationScenario
{
    public int Id { get; set; }
    public int NetworkId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>solo | dual</summary>
    public string CrewMode { get; set; } = "solo";

    /// <summary>signal | compass | geometry | pro</summary>
    public string AimMode { get; set; } = "signal";

    /// <summary>qr | login</summary>
    public string AuthMode { get; set; } = "qr";

    /// <summary>current | peak | steer</summary>
    public string DisplayMode { get; set; } = "peak";

    /// <summary>peak | hold | mindbm</summary>
    public string SuccessMode { get; set; } = "peak";

    public int? MinSignalDbm { get; set; }
    public int PeakHoldSeconds { get; set; } = 3;
    public bool RequireIpOrMac { get; set; } = true;
    public bool ShowSnrCcq { get; set; }
    public bool ShowLosHint { get; set; } = true;
    public bool ShowGeometryTargets { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
