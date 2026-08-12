using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>مكافأة أو عقوبة موظف — مع اعتماد وتطبيق على الراتب.</summary>
public class EmployeeRewardPenalty
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public int PayrollEmployeeId { get; set; }

    public EmployeeRewardPenaltyType Type { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

    [Required]
    [MaxLength(500)]
    public string Reason { get; set; } = string.Empty;

    public EmployeeRewardPenaltyStatus Status { get; set; } = EmployeeRewardPenaltyStatus.Pending;

    public int? PayrollTransactionId { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    [MaxLength(500)]
    public string? ReviewNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReviewedAt { get; set; }

    public virtual Network? CompanyNetwork { get; set; }
    public virtual PayrollEmployee? PayrollEmployee { get; set; }
    public virtual PayrollTransaction? PayrollTransaction { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ApplicationUser? ReviewedByUser { get; set; }
}
