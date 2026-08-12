using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// صلاحية (صفحة/عملية) قابلة للإسناد لمستخدم (خصوصاً الموظفين)
    /// </summary>
    public class Permission
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Key { get; set; } = string.Empty; // مثال: Sectors.Create

        [Required]
        [StringLength(200)]
        [Display(Name = "الاسم")]
        public string DisplayName { get; set; } = string.Empty; // مثال: القطاعات - إضافة

        [StringLength(100)]
        [Display(Name = "المجموعة")]
        public string? Category { get; set; } // مثال: القطاعات
    }
}

