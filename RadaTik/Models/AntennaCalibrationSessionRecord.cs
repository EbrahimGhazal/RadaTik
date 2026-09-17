namespace RadaTik.Models;

/// <summary>جلسة معايرة هوائيين تبقى بعد إعادة تشغيل التطبيق.</summary>
public sealed class AntennaCalibrationSessionRecord
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public int NetworkId { get; set; }
    public int SectorId { get; set; }
    public int? ReceiverId { get; set; }
    public string SectorName { get; set; } = string.Empty;
    public string ReceiverName { get; set; } = string.Empty;
    public double ReceiverLatitude { get; set; }
    public double ReceiverLongitude { get; set; }
    public string? ReceiverIp { get; set; }
    public string? ReceiverMac { get; set; }
    public double SectorAntennaMsl { get; set; }
    public double ReceiverAntennaMsl { get; set; }
    public string AlignmentJson { get; set; } = "{}";
    public string? PathJson { get; set; }
    /// <summary>quick | signal | pro</summary>
    public string Workflow { get; set; } = "signal";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime LastActivityUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
}
