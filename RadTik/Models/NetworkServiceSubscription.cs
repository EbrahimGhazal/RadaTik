using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum NetworkServiceSubscriptionStatus
    {
        Active = 1,
        Expired = 2,
        Suspended = 3
    }

    /// <summary>
    /// اشتراك خدمة فعّال/منتهي لشركة (Network). يُستخدم للتحكم في الوصول وإظهار روابط الـSidebar.
    /// </summary>
    public class NetworkServiceSubscription
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

        [Required]
        public PricingBillingPeriod BillingPeriod { get; set; }

        [Display(Name = "تاريخ بدء الاشتراك")]
        public DateTime StartAt { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ الاستحقاق/الانتهاء")]
        public DateTime ExpiresAt { get; set; }

        [Required]
        public NetworkServiceSubscriptionStatus Status { get; set; } = NetworkServiceSubscriptionStatus.Active;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public int? LastApprovedRequestId { get; set; }

    }
}

