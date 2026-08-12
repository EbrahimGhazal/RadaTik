using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// طلب تجديد اشتراك من نقطة التحصيل - بانتظار موافقة مدير الشركة.
/// عند القبول يتم تمديد اشتراك العميل (AccountExpirationDate).
/// </summary>
public enum CollectionPointRenewalStatus
{
    [Display(Name = "قيد الانتظار")]
    Pending = 0,
    [Display(Name = "مقبول")]
    Approved = 1,
    [Display(Name = "مرفوض")]
    Rejected = 2
}

public class CollectionPointRenewalRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }
    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    [Required]
    public int NetworkId { get; set; }
    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    /// <summary>المبلغ المحصل من العميل (تم حسمه من رصيد نقطة التحصيل)</summary>
    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>معرف نقطة التحصيل التي قدمت الطلب</summary>
    [Required]
    [StringLength(450)]
    public string RequestedByUserId { get; set; } = null!;
    [ForeignKey(nameof(RequestedByUserId))]
    public virtual ApplicationUser? RequestedByUser { get; set; }

    [Display(Name = "تاريخ الطلب")]
    public DateTime RequestedAt { get; set; } = DateTime.Now;

    [Display(Name = "الحالة")]
    public CollectionPointRenewalStatus Status { get; set; } = CollectionPointRenewalStatus.Pending;

    [StringLength(450)]
    public string? ProcessedByUserId { get; set; }
    [ForeignKey(nameof(ProcessedByUserId))]
    public virtual ApplicationUser? ProcessedByUser { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [StringLength(500)]
    public string? AdminNotes { get; set; }

    /// <summary>تاريخ انتهاء الصلاحية الجديد بعد التمديد (يُحدد عند القبول)</summary>
    [DataType(DataType.Date)]
    public DateTime? NewExpirationDate { get; set; }
}
