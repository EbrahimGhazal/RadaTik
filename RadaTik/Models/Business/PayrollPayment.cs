using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

/// <summary>دفعة راتب لموظف في شهر محدد — منفصلة عن دفتر الإيراد والمصروف.</summary>
public class PayrollPayment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public int PayrollEmployeeId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int Month { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal BaseAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Bonus { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Deduction { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsPaid { get; set; }

    public DateTime? PaidAt { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public decimal NetAmount => BaseAmount + Bonus - Deduction;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual PayrollEmployee? PayrollEmployee { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
}
