using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// صندوق نقدي يمثل المبلغ النقدي الموجود باليد لكيان (نقطة تحصيل، شبكة، مدير نظام).
    /// </summary>
    public class CashBox
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public CashBoxOwnerType OwnerType { get; set; }

        /// <summary>معرف المالك: CollectionPointAccount.Id أو Network.Id أو 0 لمدير النظام</summary>
        [Required]
        public int OwnerId { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<CashBoxWithdrawal> Withdrawals { get; set; } = new List<CashBoxWithdrawal>();
        public virtual ICollection<CashBoxDeposit> Deposits { get; set; } = new List<CashBoxDeposit>();
    }
}
