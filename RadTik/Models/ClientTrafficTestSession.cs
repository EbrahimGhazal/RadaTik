using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models;

public class ClientTrafficTestSession
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    [Required]
    public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

    [Required]
    public int DurationSeconds { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal ChargeAmount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousBalance { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal NewBalance { get; set; }

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser? CreatedByUser { get; set; }
}
