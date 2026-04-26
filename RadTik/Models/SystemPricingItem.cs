using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// نوع الخدمة المُسعّرة: شركة، شبكة، سيرفر، قطاع، لاقط، مشترك، بروفايل
    /// </summary>
    public enum PricingItemType
    {
        Company = 0,         // شركة
        Network = 1,         // شبكة
        Server = 2,          // سيرفر (MikroTik)
        Sector = 3,          // قطاع
        Receiver = 4,        // لاقط
        Subscriber = 5,      // مشترك
        Profile = 6          // بروفايل
    }

    /// <summary>
    /// مدة الاستحقاق: شهر، 3 أشهر، 6 أشهر، سنة، مرة واحدة
    /// </summary>
    public enum PricingBillingPeriod
    {
        OneTime = 0,      // مرة واحدة
        Monthly = 1,      // شهر
        Every3Months = 2, // 3 أشهر
        Every6Months = 3, // 6 أشهر
        Every12Months = 4, // سنة
        Daily = 5         // يوم
    }

    /// <summary>
    /// عملة المبلغ: ليرة سورية جديدة، قديمة، أو دولار (للتوافق مع البيانات القديمة)
    /// </summary>
    public enum PricingCurrency
    {
        SYP_New = 0,  // الليرة السورية الجديدة (ل.س.ج)
        SYP_Old = 1,  // الليرة السورية القديمة
        USD = 2       // دولار أمريكي $
    }

    /// <summary>
    /// عنصر أسعار الخدمات: الخدمة مع السعر بل.س.ج وال $
    /// </summary>
    public class SystemPricingItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "الخدمة")]
        public PricingItemType ItemType { get; set; }

        [Required]
        [Display(Name = "مدة الاستحقاق")]
        public PricingBillingPeriod BillingPeriod { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي صفر")]
        [Display(Name = "السعر ل.س.ج")]
        public decimal AmountSYP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي صفر")]
        [Display(Name = "السعر $")]
        public decimal AmountUSD { get; set; }

        /// <summary>للتوافق مع البيانات القديمة</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public PricingCurrency Currency { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
