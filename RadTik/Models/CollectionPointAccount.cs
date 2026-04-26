using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// حساب نقطة التحصيل المالي (رصيد التحصيل المتراكم)
    /// </summary>
    public class CollectionPointAccount
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [Display(Name = "معرف المستخدم")]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Display(Name = "الرصيد")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0m;

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ التحديث")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

