using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// عملية تحصيل/دفع من العميل عبر نقطة تحصيل
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
    }
}

