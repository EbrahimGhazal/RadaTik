using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Domain.FaultDiagnosis;

namespace RadaTik.Models;

public class SubscriberFaultDiagnosisRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    public int? NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    public int? MaintenanceRequestId { get; set; }

    [ForeignKey(nameof(MaintenanceRequestId))]
    public virtual MaintenanceRequest? MaintenanceRequest { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [StringLength(64)]
    public string? CreatedByUserId { get; set; }

    public SubscriberFaultComponent Cause { get; set; }

    public SubscriberFaultConfidence Confidence { get; set; }

    [Required]
    [StringLength(80)]
    public string CauseLabel { get; set; } = string.Empty;

    [Required]
    [StringLength(800)]
    public string Summary { get; set; } = string.Empty;

    [StringLength(400)]
    public string SuggestedAction { get; set; } = string.Empty;

    public MaintenanceType? SuggestedMaintenanceType { get; set; }

    public bool HasPppSession { get; set; }
    public bool HasMikroTikServer { get; set; }
    public bool ServerApiReachable { get; set; }

    public int ServerClientCount { get; set; }
    public int ServerConnectedCount { get; set; }
    public int SectorClientCount { get; set; }
    public int SectorConnectedCount { get; set; }
    public int ReceiverClientCount { get; set; }
    public int ReceiverConnectedCount { get; set; }

    [StringLength(45)]
    public string? SectorIp { get; set; }
    public bool? SectorPingOk { get; set; }
    [StringLength(120)]
    public string? SectorPingMessage { get; set; }

    [StringLength(45)]
    public string? ReceiverIp { get; set; }
    public bool? ReceiverPingOk { get; set; }
    [StringLength(120)]
    public string? ReceiverPingMessage { get; set; }

    [StringLength(45)]
    public string? ClientIp { get; set; }
    public bool? ClientPingOk { get; set; }
    [StringLength(120)]
    public string? ClientPingMessage { get; set; }

    public bool SectorRadioDegraded { get; set; }
    public int? SectorNoiseFloorDbm { get; set; }
    public int? SectorSnrDb { get; set; }
    public int? SectorCcqPercent { get; set; }

    public bool? RouterPowerOn { get; set; }
    public bool? InternetLedOn { get; set; }
    public bool? WanLedOn { get; set; }
    public bool? NeighborsOnSwitchDown { get; set; }

    public string? EvidenceJson { get; set; }

    public SubscriberFaultComponent? ConfirmedCause { get; set; }
    public MaintenanceType? ConfirmedMaintenanceType { get; set; }
    public DateTime? ConfirmedAt { get; set; }

    [StringLength(64)]
    public string? ConfirmedByUserId { get; set; }

    public bool? SuggestionMatched { get; set; }
}
