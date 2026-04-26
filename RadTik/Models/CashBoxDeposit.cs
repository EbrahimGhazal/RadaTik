using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>سجل عملية إيداع في الصندوق النقدي</summary>
    public class CashBoxDeposit
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int CashBoxId { get; set; }

        [ForeignKey(nameof(CashBoxId))]
        public virtual CashBox? CashBox { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        [Required]
        public DateTime DepositedAt { get; set; } = DateTime.Now;

        [Required]
        [StringLength(450)]
        public string DepositedByUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(DepositedByUserId))]
        public virtual ApplicationUser? DepositedByUser { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        /// <summary>طريقة الدفع (عند الإيداع التلقائي من طلب كاش)</summary>
        public int? PaymentMethodId { get; set; }

        [ForeignKey(nameof(PaymentMethodId))]
        public virtual PaymentMethod? PaymentMethod { get; set; }

        /// <summary>ربط الإيداع بطلب تغذية رصيد شركة (لمنع التكرار)</summary>
        public int? NetworkTopUpRequestId { get; set; }

        [ForeignKey(nameof(NetworkTopUpRequestId))]
        public virtual NetworkTopUpRequest? NetworkTopUpRequest { get; set; }

        /// <summary>ربط الإيداع بطلب تغذية رصيد نقطة تحصيل (لمنع التكرار)</summary>
        public int? CollectionPointTopUpRequestId { get; set; }

        [ForeignKey(nameof(CollectionPointTopUpRequestId))]
        public virtual CollectionPointTopUpRequest? CollectionPointTopUpRequest { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBefore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }
    }
}
