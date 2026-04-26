using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models;

/// <summary>
/// دفتر دائم لتتبع العناصر المفوترة لكل اشتراك خدمة
/// (لكل مستخدم/مشترك/مرسل/...).
/// يستخدم لضمان عدم إعادة خصم "المرة الأولى" بعد إعادة تشغيل الخادم.
/// </summary>
public class ServiceUnitChargeLedger
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Required]
    public int NetworkServiceSubscriptionId { get; set; }

    [ForeignKey(nameof(NetworkServiceSubscriptionId))]
    public virtual NetworkServiceSubscription? Subscription { get; set; }

    [Required]
    public PricingChargeUnit ChargeUnit { get; set; }

    [Required]
    [StringLength(128)]
    public string UnitEntityKey { get; set; } = string.Empty;

    [Required]
    public bool IsActive { get; set; } = true;

    public DateTime? FirstChargedAt { get; set; }
    public DateTime? LastChargedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
