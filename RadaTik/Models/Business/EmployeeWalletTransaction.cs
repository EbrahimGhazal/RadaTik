using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

public enum EmployeeWalletTransactionSource
{
    [Display(Name = "تغذية — موافقة طلب")]
    TopUpRequestApproved = 1,

    [Display(Name = "تغذية — مباشرة من مدير الشركة")]
    DirectTopUpByManager = 2
}

/// <summary>سجل حركات محفظة الموظف النقدية (منفصلة عن مستحقات الراتب).</summary>
public class EmployeeWalletTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public int PayrollEmployeeId { get; set; }

    [ForeignKey(nameof(PayrollEmployeeId))]
    public virtual PayrollEmployee? PayrollEmployee { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousBalance { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal NewBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlatformCommissionAmount { get; set; }

    [Required]
    public EmployeeWalletTransactionSource Source { get; set; }

    public int? EmployeeWalletTopUpRequestId { get; set; }

    [ForeignKey(nameof(EmployeeWalletTopUpRequestId))]
    public virtual EmployeeWalletTopUpRequest? EmployeeWalletTopUpRequest { get; set; }

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(500)]
    public string? Notes { get; set; }
}
