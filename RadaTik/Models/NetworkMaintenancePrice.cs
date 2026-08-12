using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// Fixed maintenance prices per company network and maintenance type.
/// </summary>
public class NetworkMaintenancePrice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    [Required]
    public MaintenanceType MaintenanceType { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, double.MaxValue)]
    public decimal AmountSYP { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    [StringLength(450)]
    public string UpdatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UpdatedByUserId))]
    public virtual ApplicationUser? UpdatedByUser { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
