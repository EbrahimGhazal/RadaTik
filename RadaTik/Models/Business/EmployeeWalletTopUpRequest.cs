using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

public enum EmployeeWalletTopUpRequestStatus
{
    [Display(Name = "قيد الانتظار")]
    Pending = 1,

    [Display(Name = "مقبول")]
    Approved = 2,

    [Display(Name = "مرفوض")]
    Rejected = 3,

    [Display(Name = "ملغي")]
    Cancelled = 4
}

public enum EmployeeWalletTopUpRequestSource
{
    [Display(Name = "طلب موظف")]
    EmployeeSelf = 1,

    [Display(Name = "طلب مدير الشركة")]
    CompanyManager = 2
}

/// <summary>طلب تغذية محفظة الموظف — يُخصم من محفظة الشركة عند الموافقة (+ عمولة المنصة إن وُجدت).</summary>
public class EmployeeWalletTopUpRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [ForeignKey(nameof(CompanyNetworkId))]
    public virtual Network? CompanyNetwork { get; set; }

    [Required]
    public int PayrollEmployeeId { get; set; }

    [ForeignKey(nameof(PayrollEmployeeId))]
    public virtual PayrollEmployee? PayrollEmployee { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 100000000)]
    public decimal Amount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PlatformCommissionAmount { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public EmployeeWalletTopUpRequestStatus Status { get; set; } = EmployeeWalletTopUpRequestStatus.Pending;

    [Required]
    public EmployeeWalletTopUpRequestSource RequestSource { get; set; } = EmployeeWalletTopUpRequestSource.EmployeeSelf;

    [Required]
    [StringLength(450)]
    public string RequestedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(RequestedByUserId))]
    public virtual ApplicationUser? RequestedByUser { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? ProcessedByUserId { get; set; }

    [ForeignKey(nameof(ProcessedByUserId))]
    public virtual ApplicationUser? ProcessedByUser { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [StringLength(500)]
    public string? AdminNotes { get; set; }
}
