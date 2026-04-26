using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models;

/// <summary>
/// جهة معالجة طلب تغذية رصيد المشترك (مدير الشركة أو نقطة تحصيل محددة).
/// </summary>
public enum ClientWalletTopUpRecipientTarget
{
    [Display(Name = "مدير الشركة")]
    CompanyManager = 1,

    [Display(Name = "نقطة تحصيل")]
    CollectionPoint = 2
}

public enum ClientWalletTopUpRequestStatus
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

/// <summary>
/// طلب تغذية رصيد محفظة المشترك (يقدمه العميل عبر البوابة).
/// </summary>
public class ClientWalletTopUpRequest
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    /// <summary>شبكة العميل وقت الطلب</summary>
    [Required]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    [Required]
    public ClientWalletTopUpRecipientTarget RecipientTarget { get; set; } = ClientWalletTopUpRecipientTarget.CompanyManager;

    /// <summary>مطلوب عند <see cref="RecipientTarget"/> = CollectionPoint</summary>
    public int? TargetCollectionPointAccountId { get; set; }

    [ForeignKey(nameof(TargetCollectionPointAccountId))]
    public virtual CollectionPointAccount? TargetCollectionPointAccount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
    public decimal Amount { get; set; }

    [Required]
    public int PaymentMethodId { get; set; }

    [ForeignKey(nameof(PaymentMethodId))]
    public virtual PaymentMethod? PaymentMethod { get; set; }

    [StringLength(200)]
    [Display(Name = "رقم الإشعار/المرجع")]
    public string? ReferenceNumber { get; set; }

    [StringLength(500)]
    [Display(Name = "صورة الإيصال")]
    public string? ReceiptImagePath { get; set; }

    [StringLength(1000)]
    public string? Notes { get; set; }

    [Required]
    public ClientWalletTopUpRequestStatus Status { get; set; } = ClientWalletTopUpRequestStatus.Pending;

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
