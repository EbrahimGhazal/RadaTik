using System.ComponentModel.DataAnnotations;

namespace RadTik.ViewModels.Admin
{
    public class EditEmployeeViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

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

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// تغيير كلمة المرور (اختياري)
        /// </summary>
        [StringLength(100, ErrorMessage = "كلمة المرور يجب أن تكون على الأقل {2} أحرف", MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة مرور جديدة")]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور الجديدة")]
        [Compare("NewPassword", ErrorMessage = "كلمة المرور الجديدة وتأكيدها غير متطابقتين")]
        public string? ConfirmNewPassword { get; set; }

        /// <summary>
        /// الصلاحيات المختارة (IDs) من جدول Permissions.
        /// </summary>
        [Display(Name = "الصلاحيات")]
        public List<int> SelectedPermissionIds { get; set; } = [];

        public string? ReturnUrl { get; set; }
    }
}

