using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum NetworkWalletTransactionType
    {
        TopUp = 1,
        ServiceCharge = 2,
        Refund = 3,
        Adjustment = 4,
        CollectionCommission = 5,
        MaintenanceRevenue = 6
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
        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousBalance { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NewBalance { get; set; }

        public int? NetworkServiceRequestId { get; set; }
        public int? NetworkTopUpRequestId { get; set; }
        public int? NetworkServiceSubscriptionId { get; set; }

        /// <summary>عملية تحصيل عميل مرتبطة بعمولة التحصيل.</summary>
        public int? RelatedPaymentTransactionId { get; set; }

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

