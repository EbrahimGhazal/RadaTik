using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>سجل عملية سحب من الصندوق النقدي</summary>
    public class CashBoxWithdrawal
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
        public DateTime WithdrawnAt { get; set; } = DateTime.Now;

        [Required]
        [StringLength(450)]
        public string WithdrawnByUserId { get; set; } = string.Empty;

        [ForeignKey(nameof(WithdrawnByUserId))]
        public virtual ApplicationUser? WithdrawnByUser { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات / سبب السحب")]
        public string? Notes { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBefore { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }
    }
}
