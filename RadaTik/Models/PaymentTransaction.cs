using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// عملية تحصيل/دفع من العميل عبر نقطة تحصيل.
    /// Amount = المبلغ النقدي المحصّل (للتوافق مع التقارير القديمة، عادةً ل.س.ج).
    /// </summary>
    public class PaymentTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "العميل")]
        public int ClientId { get; set; }

        [ForeignKey(nameof(ClientId))]
        public virtual Client? Client { get; set; }

        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Required]
        [Display(Name = "المبلغ")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
        public decimal Amount { get; set; }

        [Required]
        [Display(Name = "مبلغ الدفع")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PaymentAmount { get; set; }

        [Required]
        [Display(Name = "عملة الدفع")]
        public PricingCurrency PaymentCurrency { get; set; } = PricingCurrency.SYP_New;

        [Required]
        [Display(Name = "مبلغ الحساب")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AccountAmount { get; set; }

        [Required]
        [Display(Name = "عملة الحساب")]
        public PricingCurrency AccountCurrency { get; set; } = PricingCurrency.SYP_New;

        /// <summary>1 USD = ExchangeRate ل.س.ج عند التحصيل (null إذا نفس العملة).</summary>
        [Display(Name = "سعر الصرف")]
        [Column(TypeName = "decimal(18,4)")]
        public decimal? ExchangeRate { get; set; }

        [Display(Name = "تاريخ التحصيل")]
        public DateTime PaymentDate { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "تم التحصيل بواسطة")]
        public string ReceivedByUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(ReceivedByUserId))]
        public virtual ApplicationUser? ReceivedByUser { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "نوع العملية")]
        [StringLength(40)]
        public string OperationType { get; set; } = "ReceivePayment";

        [Display(Name = "رقم مرجعي")]
        [StringLength(40)]
        public string? ReferenceNumber { get; set; }

        [Display(Name = "رصيد العميل قبل")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousClientBalance { get; set; }

        [Display(Name = "رصيد العميل بعد")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NewClientBalance { get; set; }

        [Display(Name = "رصيد نقطة التحصيل قبل")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousPointBalance { get; set; }

        [Display(Name = "رصيد نقطة التحصيل بعد")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NewPointBalance { get; set; }

        /// <summary>المبلغ المحصّل نقداً بالليرة (لعمولة المنصة ومحفظة الشركة).</summary>
        [NotMapped]
        public decimal CollectionAmountSyp =>
            PaymentCurrency is PricingCurrency.SYP_New or PricingCurrency.SYP_Old
                ? PaymentAmount
                : AccountCurrency is PricingCurrency.SYP_New or PricingCurrency.SYP_Old
                    ? AccountAmount
                    : PaymentAmount;
    }
}
