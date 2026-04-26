using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum NetworkServiceRequestStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }

    /// <summary>
    /// طلب اشتراك/تفعيل خدمة من مدير الشركة إلى مدير النظام.
    /// قد يتضمن الطلب خصماً فورياً أو خصماً مؤجلاً بعد الموافقة وفق سياسة الخدمة.
    /// </summary>
    public class NetworkServiceRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int NetworkId { get; set; } // الشركة الرئيسية (ParentNetworkId == null)

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Required]
        [StringLength(100)]
        public string FeatureKey { get; set; } = string.Empty;

        public int? FeaturePricingId { get; set; }

        [ForeignKey(nameof(FeaturePricingId))]
        public virtual FeaturePricing? FeaturePricing { get; set; }

        [Required]
        public PricingBillingPeriod BillingPeriod { get; set; }

        // Snapshot pricing at request time (immutable for audit even if pricing changes later)
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountSYP { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountUSD { get; set; }

        public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

        [Required]
        public NetworkServiceRequestStatus Status { get; set; } = NetworkServiceRequestStatus.Pending;

        [Required]
        [StringLength(450)]
        public string RequestedByUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(RequestedByUserId))]
        public virtual ApplicationUser? RequestedByUser { get; set; }

        public DateTime RequestedAt { get; set; } = DateTime.Now;

        [StringLength(450)]
        public string? DecidedByUserId { get; set; }

        [ForeignKey(nameof(DecidedByUserId))]
        public virtual ApplicationUser? DecidedByUser { get; set; }

        public DateTime? DecidedAt { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public int? ChargeWalletTransactionId { get; set; }

        public int? RefundWalletTransactionId { get; set; }
    }
}

