using System.ComponentModel.DataAnnotations;

namespace RadTik.ViewModels.Admin
{
    public class CreateEmployeeViewModel
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "صيغة البريد الإلكتروني غير صحيحة")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "الاسم الكامل")]
        public string? FullName { get; set; }

        [Display(Name = "رقم الجوال")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [StringLength(100, ErrorMessage = "كلمة المرور يجب أن تكون على الأقل {2} أحرف", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// الصلاحيات المختارة (IDs) من جدول Permissions.
        /// </summary>
        [Display(Name = "الصلاحيات")]
        public List<int> SelectedPermissionIds { get; set; } = [];

        /// <summary>
        /// رابط العودة بعد الإنشاء.
        /// </summary>
        public string? ReturnUrl { get; set; }
    }
}

