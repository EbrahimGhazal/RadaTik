using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// جهة طلب التغذية (تاريخياً: مدير النظام أو مدير الشركة). حالياً تُستخدم قيمة مدير النظام فقط.
/// </summary>
public enum CollectionPointTopUpTarget
{
    [Display(Name = "مدير النظام")]
    SystemAdmin = 1,
    [Display(Name = "مدير الشركة")]
    CompanyManager = 2
}

/// <summary>
/// طلب تغذية رصيد نقطة التحصيل (يقدمه نقطة التحصيل ويوافق عليه مدير النظام).
/// </summary>
public enum CollectionPointTopUpStatus
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

public class CollectionPointTopUpRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    /// <summary>معرف حساب نقطة التحصيل</summary>
    [Required]
    public int CollectionPointAccountId { get; set; }

    [ForeignKey(nameof(CollectionPointAccountId))]
    public virtual CollectionPointAccount? CollectionPointAccount { get; set; }

    /// <summary>جهة الطلب: مدير النظام أو مدير الشركة</summary>
    [Required]
    public CollectionPointTopUpTarget RequestTargetType { get; set; } = CollectionPointTopUpTarget.SystemAdmin;

    /// <summary>معرف الشبكة/الشركة المستهدفة (عند RequestTargetType=CompanyManager)</summary>
    public int? TargetNetworkId { get; set; }

    [ForeignKey(nameof(TargetNetworkId))]
    public virtual Network? TargetNetwork { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
    public decimal Amount { get; set; }

    /// <summary>طريقة الدفع (مرجع لجدول طرق الدفع) - اختياري للتوافق مع البيانات القديمة</summary>
    public int? PaymentMethodId { get; set; }

    [ForeignKey(nameof(PaymentMethodId))]
    public virtual PaymentMethod? PaymentMethod { get; set; }

    /// <summary>اسم/وصف طريقة الدفع (عند عدم وجود طرق دفع معرفة)</summary>
    [StringLength(200)]
    public string? Method { get; set; }

    /// <summary>رقم إيصال/مرجع (اختياري)</summary>
    [StringLength(100)]
    public string? ReferenceNumber { get; set; }

    /// <summary>مسار صورة الإيصال (مثلاً /uploads/receipts/xxx.jpg)</summary>
    [StringLength(500)]
    [Display(Name = "صورة الإيصال")]
    public string? ReceiptImagePath { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public CollectionPointTopUpStatus Status { get; set; } = CollectionPointTopUpStatus.Pending;

    [Required]
    [StringLength(450)]
    public string RequestedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(RequestedByUserId))]
    public virtual ApplicationUser? RequestedByUser { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.Now;

    [StringLength(450)]
    public string? ProcessedByUserId { get; set; }

    [ForeignKey(nameof(ProcessedByUserId))]
    public virtual ApplicationUser? ProcessedByUser { get; set; }

    public DateTime? ProcessedAt { get; set; }

    [StringLength(500)]
    public string? AdminNotes { get; set; }
}
