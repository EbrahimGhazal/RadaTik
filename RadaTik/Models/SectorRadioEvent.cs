using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public class SectorRadioEvent
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int SectorId { get; set; }

    [ForeignKey(nameof(SectorId))]
    public virtual Sector? Sector { get; set; }

    public long? MetricSampleId { get; set; }

    [ForeignKey(nameof(MetricSampleId))]
    public virtual SectorRadioMetricSample? MetricSample { get; set; }

    [Required]
    [StringLength(16)]
    public string Severity { get; set; } = "Warning";

    [Required]
    [StringLength(32)]
    public string EventType { get; set; } = "Threshold";

    [StringLength(64)]
    public string MetricName { get; set; } = string.Empty;

    public decimal? MetricValue { get; set; }
    public decimal? ThresholdValue { get; set; }

    [Required]
    [StringLength(400)]
    public string Message { get; set; } = string.Empty;

    public bool IsAcknowledged { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
