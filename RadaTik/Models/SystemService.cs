using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// تعريف خدمة (قابلة للإضافة/الإلغاء من مدير النظام).
    /// هذه الخدمات تُستخدم لتوليد صفحات عامة (Index/Create/Edit) عبر CustomServicesController.
    /// </summary>
    public class SystemService
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        /// <summary>
        /// مفتاح الخدمة (Unique) - يُستخدم في الاشتراكات والتسعير والروابط.
        /// مثال: CUSTOM:ShamCashReports
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم الخدمة")]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(1000)]
        [Display(Name = "وصف")]
        public string? Description { get; set; }

        [StringLength(100)]
        [Display(Name = "أيقونة (FontAwesome)")]
        public string? IconClass { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

