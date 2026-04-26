using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// بيانات عامة لكل خدمة مخصصة (Generic CRUD) ضمن شركة معينة.
    /// </summary>
    public class CustomServiceItem
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int NetworkId { get; set; } // الشركة الرئيسية

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        [Required]
        [StringLength(100)]
        public string ServiceKey { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        [Display(Name = "العنوان")]
        public string Title { get; set; } = string.Empty;

        [StringLength(2000)]
        [Display(Name = "الوصف/المحتوى")]
        public string? Body { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

