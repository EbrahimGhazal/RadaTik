using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;

namespace RadaTik.Models
{
    public class MikroTikServer
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المضيف مطلوب")]
        [Display(Name = "اسم المضيف")]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$|^([a-zA-Z0-9]([a-zA-Z0-9\-]{0,61}[a-zA-Z0-9])?\.)+[a-zA-Z]{2,}$",
            ErrorMessage = "يرجى إدخال عنوان IP صالح أو اسم نطاق")]
        public string Host { get; set; } = null!;

        [Required(ErrorMessage = "اسم الخادم مطلوب")]
        [Display(Name = "اسم الخادم")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "يجب أن يكون اسم الخادم بين 2 و 100 حرف")]
        public string Name { get; set; } = null!;

        [Required(ErrorMessage = "المنفذ مطلوب")]
        [Display(Name = "المنفذ")]
        [Range(1, 65535, ErrorMessage = "يجب أن يكون المنفذ بين 1 و 65535")]
        public int Port { get; set; } = 8728;

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        public string User { get; set; } = null!;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        [MinLength(1, ErrorMessage = "كلمة المرور مطلوبة")]
        public string Pass { get; set; } = null!;

        [Display(Name = "ملاحظات")]
        [DataType(DataType.MultilineText)]
        [StringLength(500, ErrorMessage = "لا يمكن أن تتجاوز الملاحظات 500 حرف")]
        public string? Notes { get; set; }

        [Display(Name = "معرف المستخدم")]
        [StringLength(50, ErrorMessage = "لا يمكن أن يتجاوز معرف المستخدم 50 حرف")]
        public string? UserID { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ التحديث")]
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        // علاقة مع Network
        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey("NetworkId")]
        public virtual Network? Network { get; set; }

        // علاقة التنقل
        public virtual ICollection<Sector> Sectors { get; set; } = new List<Sector>();
    }
}