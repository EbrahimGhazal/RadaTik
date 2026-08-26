using System.ComponentModel.DataAnnotations;

namespace RadaTik.ViewModels.ClientPortal
{
    /// <summary>نموذج تعديل بروفايل العميل (البيانات الشخصية فقط — بدون اسم مستخدم أو كلمة مرور MikroTik).</summary>
    public class ClientProfileViewModel
    {
        public int ClientId { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [Display(Name = "الاسم الكامل")]
        [StringLength(100, ErrorMessage = "الاسم يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = "";

        [Display(Name = "البريد الإلكتروني")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [StringLength(256)]
        public string? Email { get; set; }

        [Required(ErrorMessage = "رقم الجوال مطلوب")]
        [Display(Name = "رقم الجوال")]
        [StringLength(15, ErrorMessage = "رقم الجوال يجب أن لا يتجاوز 15 رقماً")]
        [RegularExpression(@"^[\d\s\-\+]+$", ErrorMessage = "رقم الجوال يجب أن يحتوي على أرقام فقط")]
        public string PhoneNumber { get; set; } = "";

        [Display(Name = "مكان السكن")]
        [StringLength(500, ErrorMessage = "مكان السكن يجب أن لا يتجاوز 500 حرف")]
        public string? ResidenceAddress { get; set; }

        [Display(Name = "خط العرض")]
        public double? Latitude { get; set; }

        [Display(Name = "خط الطول")]
        public double? Longitude { get; set; }

        [Display(Name = "اسم دخول النظام")]
        public string? SystemUserName { get; set; }

        [Display(Name = "اسم المستخدم على MikroTik")]
        public string? MikroTikUserName { get; set; }

        /// <summary>عرض فقط: يُحدَّد من شركة الإدارة ولا يُعدَّل من بوابة العميل.</summary>
        public bool IsVip { get; set; }

        public string? VipNote { get; set; }

        public DateTime? VipSince { get; set; }
    }
}
