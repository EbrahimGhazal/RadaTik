using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    public enum NetworkWalletTransactionType
    {
        TopUp = 1,
        ServiceCharge = 2,
        Refund = 3,
        Adjustment = 4,
        CollectionCommission = 5,
        MaintenanceRevenue = 6,
        /// <summary>إيراد تحصيل اشتراك/دفعة قبل خصم عمولة المنصة (يُتبع غالباً بسجل عمولة).</summary>
        SubscriptionCollectedRevenue = 7,

        /// <summary>دفع فاتورة شراء مواد من محفظة الشركة.</summary>
        MaterialPurchasePayment = 8,

        /// <summary>استرداد مبلغ فاتورة شراء مواد (إلغاء أو إلغاء الدفع).</summary>
        MaterialPurchaseRefund = 9,

        /// <summary>تحصيل فاتورة بيع مواد إلى محفظة الشركة.</summary>
        MaterialSaleReceipt = 10,

        /// <summary>عكس تحصيل فاتورة بيع مواد.</summary>
        MaterialSaleRefund = 11,

        /// <summary>تغذية محفظة الشركة من الصندوق النقدي (تحويل تنظيمي داخلي).</summary>
        WalletFundedFromCashBox = 12
    }

    /// <summary>
    /// سجل عمليات محفظة الشركة. SignedAmount موجب للإيداع وسالب للخصم.
    /// </summary>
    public class NetworkWalletTransaction
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int NetworkId { get; set; } // الشركة الرئيسية (ParentNetworkId == null)

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Required]
        public NetworkWalletTransactionType Type { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "المبلغ (موجب/سالب)")]
        public decimal SignedAmount { get; set; }

        [Required]
        [Display(Name = "عملة المحفظة")]
        public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousBalance { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NewBalance { get; set; }

        public int? NetworkServiceRequestId { get; set; }
        public int? NetworkTopUpRequestId { get; set; }
        public int? NetworkServiceSubscriptionId { get; set; }

        public int? MaterialPurchaseInvoiceId { get; set; }

        public int? MaterialSalesInvoiceId { get; set; }

        /// <summary>عملية تحصيل عميل مرتبطة بعمولة التحصيل.</summary>
        public int? RelatedPaymentTransactionId { get; set; }

        public int? EmployeeWalletTopUpRequestId { get; set; }

        [ForeignKey(nameof(RelatedPaymentTransactionId))]
        public virtual PaymentTransaction? RelatedPaymentTransaction { get; set; }

        [Required]
        [StringLength(450)]
        public string CreatedByUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(CreatedByUserId))]
        public virtual ApplicationUser? CreatedByUser { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(1000)]
        public string? Notes { get; set; }
    }
}

