using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// تسعير ميزات/خدمات النظام (FeatureKeys.*) التي يمكن للشركات الاشتراك بها.
    /// </summary>
    public class FeaturePricing
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// مفتاح الخدمة/الميزة (مثل: "Clients", "Sectors"...). يجب أن يطابق FeatureKeys.
        /// </summary>
        [Required]
        [StringLength(100)]
        public string FeatureKey { get; set; } = string.Empty;

        [Required]
        [Display(Name = "مدة الاستحقاق")]
        public PricingBillingPeriod BillingPeriod { get; set; }

        /// <summary>
        /// طريقة احتساب السعر: ثابت للشركة أو لكل مستخدم... إلخ.
        /// </summary>
        [Required]
        [Display(Name = "لكل")]
        public PricingChargeUnit ChargeUnit { get; set; } = PricingChargeUnit.Flat;

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي صفر")]
        [Display(Name = "السعر ل.س.ج")]
        public decimal AmountSYP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من أو يساوي صفر")]
        [Display(Name = "السعر $")]
        public decimal AmountUSD { get; set; }

        /// <summary>
        /// العملة الافتراضية المستخدمة عند الخصم (عادةً SYP_New).
        /// </summary>
        public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

