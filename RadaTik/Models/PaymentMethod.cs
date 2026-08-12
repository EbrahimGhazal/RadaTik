using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// طرق الدفع/التعبئة القابلة للإدارة من مدير النظام (إضافة/إلغاء/ترتيب).
    /// </summary>
    public class PaymentMethod
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم الطريقة")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// هل تعتبر هذه الطريقة "نقد/كاش" (تؤثر على الخزنة النقدية)؟
        /// </summary>
        [Display(Name = "كاش")]
        public bool IsCash { get; set; } = false;

        [Display(Name = "الترتيب")]
        public int DisplayOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

