using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum NotificationType
    {
        SubscriptionExpiring = 1,
        MaintenanceRequestSubmitted = 2,
        SpeedChangeRequestSubmitted = 3,
        ClientJoinRequestSubmitted = 4,
        EmployeeJoinRequestSubmitted = 5,
        ClientTopUpSubmitted = 6,
        CollectionPointTopUpRequestSubmitted = 7,
        ClientWalletTopUpRequestSubmitted = 8,
        MaintenanceInvoiceIssued = 9,
        MaintenanceInvoicePaid = 10
    }

    /// <summary>
    /// إشعارات داخلية للمستخدمين (مثل تنبيه قرب انتهاء اشتراك خدمة).
    /// </summary>
    public class UserNotification
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Key { get; set; } = string.Empty; // unique key to prevent duplicates

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        public int? NetworkId { get; set; }

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Required]
        public NotificationType Type { get; set; } = NotificationType.SubscriptionExpiring;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(1000)]
        public string Message { get; set; } = string.Empty;

        public int? NetworkServiceSubscriptionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsRead { get; set; } = false;

        public DateTime? ReadAt { get; set; }
    }
}

