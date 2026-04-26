using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum NetworkTopUpRequestStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3,
        Cancelled = 4
    }

    /// <summary>
    /// طلب تغذية رصيد الشركة (يقدمه مدير الشركة ويوافق عليه مدير النظام).
    /// </summary>
    public class NetworkTopUpRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int NetworkId { get; set; } // الشركة الرئيسية

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, 100000000, ErrorMessage = "المبلغ غير صحيح")]
        public decimal Amount { get; set; }

        [Display(Name = "طريقة الدفع/التعبئة")]
        public int? PaymentMethodId { get; set; }

        [ForeignKey(nameof(PaymentMethodId))]
        public virtual PaymentMethod? PaymentMethod { get; set; }

        [StringLength(200)]
        [Display(Name = "طريقة الدفع/التعبئة (نص)")]
        public string? Method { get; set; }

        [StringLength(200)]
        [Display(Name = "رقم المرجع/الإيصال")]
        public string? ReferenceNumber { get; set; }

        /// <summary>مسار صورة الإيصال (مثلاً /uploads/receipts/xxx.jpg)</summary>
        [StringLength(500)]
        [Display(Name = "صورة الإيصال")]
        public string? ReceiptImagePath { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        [Required]
        public NetworkTopUpRequestStatus Status { get; set; } = NetworkTopUpRequestStatus.Pending;

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

        public int? ApprovedWalletTransactionId { get; set; }
    }
}

