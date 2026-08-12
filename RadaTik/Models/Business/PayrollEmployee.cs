using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>موظف للرواتب — يمكن ربطه بحساب دخول للنظام.</summary>
public class PayrollEmployee
{
    public const decimal FullTimeWeeklyHoursDefault = 40m;

    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    /// <summary>حساب الدخول للنظام (اختياري).</summary>
    [MaxLength(450)]
    public string? ApplicationUserId { get; set; }

    [Required]
    [MaxLength(120)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(80)]
    public string? JobTitle { get; set; }

    [MaxLength(30)]
    public string? Phone { get; set; }

    [Required]
    public PayrollEmploymentType EmploymentType { get; set; } = PayrollEmploymentType.FullTime;

    /// <summary>ساعات العمل الأسبوعية (مهمة للدوام الجزئي).</summary>
    [Column(TypeName = "decimal(8,2)")]
    public decimal WeeklyWorkHours { get; set; } = FullTimeWeeklyHoursDefault;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlySalary { get; set; }

    /// <summary>رصيد المحفظة النقدية — يُعبَّأ فقط عبر طلب معتمد أو تغذية مباشرة من مدير الشركة.</summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal WalletBalance { get; set; }

    public DateTime? HireDate { get; set; }

    /// <summary>تاريخ انتهاء الخدمة (يُستخدم لاحتساب الراتب حتى هذا اليوم).</summary>
    public DateTime? TerminationDate { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ApplicationUser? ApplicationUser { get; set; }
    public virtual ICollection<PayrollPayment> Payments { get; set; } = new List<PayrollPayment>();
    public virtual ICollection<PayrollTransaction> Transactions { get; set; } = new List<PayrollTransaction>();
    public virtual ICollection<PayrollSalaryRevision> SalaryRevisions { get; set; } = new List<PayrollSalaryRevision>();
    public virtual ICollection<PayrollWithdrawalRequest> WithdrawalRequests { get; set; } = new List<PayrollWithdrawalRequest>();
    public virtual ICollection<EmployeeWalletTransaction> WalletTransactions { get; set; } = new List<EmployeeWalletTransaction>();
    public virtual ICollection<EmployeeWalletTopUpRequest> WalletTopUpRequests { get; set; } = new List<EmployeeWalletTopUpRequest>();
}
