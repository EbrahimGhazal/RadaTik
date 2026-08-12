using System.ComponentModel.DataAnnotations;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.ViewModels.Admin
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
        [StringLength(100, ErrorMessage = "كلمة المرور يجب أن تكون على الأقل {2} أحرف", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقتين")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "قسم الموظف")]
        public EmployeeDepartment Department { get; set; } = EmployeeDepartment.None;

        /// <summary>
        /// الصلاحيات المختارة (IDs) من جدول Permissions.
        /// </summary>
        [Display(Name = "الصلاحيات")]
        public List<int> SelectedPermissionIds { get; set; } = [];

        /// <summary>
        /// رابط العودة بعد الإنشاء.
        /// </summary>
        public string? ReturnUrl { get; set; }

        [Display(Name = "إنشاء سجل رواتب مرتبط")]
        public bool SyncToPayroll { get; set; }

        [Display(Name = "الراتب الشهري (ل.س.ج جديدة)")]
        [Range(0, double.MaxValue, ErrorMessage = "الراتب يجب أن يكون موجباً")]
        public decimal? MonthlySalary { get; set; }

        [Display(Name = "نوع الدوام")]
        public PayrollEmploymentType PayrollEmploymentType { get; set; } = PayrollEmploymentType.FullTime;

        [Display(Name = "ساعات العمل الأسبوعية (دوام جزئي)")]
        [Range(1, 80, ErrorMessage = "أدخل ساعات أسبوعية بين 1 و 80")]
        public decimal? WeeklyWorkHours { get; set; }

        [Display(Name = "المسمى الوظيفي (رواتب)")]
        public string? PayrollJobTitle { get; set; }
    }
}

