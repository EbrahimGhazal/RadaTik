using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// مصدر تغذية رصيد العميل: مدير النظام، مدير الشبكة، نقطة التحصيل
/// </summary>
public enum ClientTopUpSource
{
    [Display(Name = "مدير النظام")]
    SystemAdmin = 1,
    [Display(Name = "مدير الشبكة")]
    NetworkManager = 2,
    [Display(Name = "نقطة التحصيل")]
    CollectionPoint = 3,

    [Display(Name = "طلب من بوابة المشترك")]
    ClientPortalRequest = 4
}

/// <summary>
/// عملية تغذية رصيد العميل (المشترك)
/// </summary>
public class ClientTopUpTransaction
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
    public decimal Amount { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousBalance { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal NewBalance { get; set; }

    [Required]
    public ClientTopUpSource SourceType { get; set; }

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [StringLength(500)]
    public string? Notes { get; set; }

    /// <summary>معرف الشبكة (عند SourceType=NetworkManager - تم الخصم من رصيد الشبكة)</summary>
    public int? NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    /// <summary>معرف حساب نقطة التحصيل (عند SourceType=CollectionPoint - تم الخصم من رصيد نقطة التحصيل)</summary>
    public int? CollectionPointAccountId { get; set; }

    [ForeignKey(nameof(CollectionPointAccountId))]
    public virtual CollectionPointAccount? CollectionPointAccount { get; set; }
}
