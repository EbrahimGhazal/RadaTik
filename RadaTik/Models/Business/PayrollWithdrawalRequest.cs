using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

public enum PayrollWithdrawalRequestStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4
}

/// <summary>طلب سحب رصيد من محفظة الراتب — يقدّمه الموظف ويعتمده مدير الرواتب.</summary>
public class PayrollWithdrawalRequest
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
    public decimal Amount { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    public PayrollWithdrawalRequestStatus Status { get; set; } = PayrollWithdrawalRequestStatus.Pending;

    [Required]
    [MaxLength(450)]
    public string RequestedByUserId { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(500)]
    public string? ReviewNotes { get; set; }

    public int? PayrollTransactionId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual PayrollEmployee? PayrollEmployee { get; set; }
    public virtual ApplicationUser? RequestedByUser { get; set; }
    public virtual ApplicationUser? ReviewedByUser { get; set; }
    public virtual PayrollTransaction? PayrollTransaction { get; set; }
}
