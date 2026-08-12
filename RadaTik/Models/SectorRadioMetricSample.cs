using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public class SectorRadioMetricSample
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int SectorId { get; set; }

    [ForeignKey(nameof(SectorId))]
    public virtual Sector? Sector { get; set; }

    public int? MikroTikServerId { get; set; }

    [ForeignKey(nameof(MikroTikServerId))]
    public virtual MikroTikServer? MikroTikServer { get; set; }

    [Required]
    public DateTime CapturedAt { get; set; } = DateTime.Now;

    public int? FrequencyMhz { get; set; }
    public int? ChannelWidthMhz { get; set; }
    public int? NoiseFloorDbm { get; set; }
    public int? SignalDbm { get; set; }
    public int? SnrDb { get; set; }
    public int? CcqPercent { get; set; }
    public decimal? TxRateMbps { get; set; }
    public decimal? RxRateMbps { get; set; }

    [StringLength(40)]
    public string Source { get; set; } = "MikroTik";

    [StringLength(500)]
    public string? StatusMessage { get; set; }
}
