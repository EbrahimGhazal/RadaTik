namespace RadaTik.Models;

/// <summary>جلسة معايرة حية محفوظة لتبقى بعد إعادة التشغيل.</summary>
public sealed class CalibrationSessionRecord
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int NetworkId { get; set; }
    public int SectorId { get; set; }
    public int? ReceiverId { get; set; }
    public int? ScenarioId { get; set; }
    public string ScenarioName { get; set; } = string.Empty;
    public string ScenarioJson { get; set; } = "{}";
    public string SectorName { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public double ReceiverLatitude { get; set; }
    public double ReceiverLongitude { get; set; }
    public string? ReceiverIp { get; set; }
    public string? ReceiverMac { get; set; }
    public double SectorAntennaMsl { get; set; }
    public double ReceiverAntennaMsl { get; set; }
    public string AlignmentJson { get; set; } = "{}";
    public string? PathSummary { get; set; }
    public bool PathClear { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(4);
}
