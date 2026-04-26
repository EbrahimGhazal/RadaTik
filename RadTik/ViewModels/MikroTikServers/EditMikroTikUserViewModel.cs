using System.ComponentModel.DataAnnotations;

namespace RadTik.ViewModels.MikroTikServers
{
    public class EditMikroTikUserViewModel
    {
        [Required(ErrorMessage = "معرف السيرفر مطلوب")]
        public int MikroTikServerId { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        [StringLength(100, ErrorMessage = "اسم المستخدم يجب أن لا يتجاوز 100 حرف")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "كلمة المرور")]
        [StringLength(255, ErrorMessage = "كلمة المرور طويلة جداً")]
        public string? Password { get; set; }

        [Required(ErrorMessage = "اسم العميل مطلوب")]
        [Display(Name = "اسم العميل")]
        [StringLength(100, ErrorMessage = "الاسم يجب أن لا يتجاوز 100 حرف")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        [StringLength(15, ErrorMessage = "رقم الهاتف يجب أن لا يتجاوز 15 رقماً")]
        [Phone(ErrorMessage = "رقم الهاتف غير صالح")]
        public string? PhoneNumber { get; set; }

        [Required(ErrorMessage = "البروفايل مطلوب")]
        [Display(Name = "باقة السرعة")]
        [StringLength(100, ErrorMessage = "اسم البروفايل يجب أن لا يتجاوز 100 حرف")]
        public string ProfileName { get; set; } = string.Empty;

        [Display(Name = "المستلم المسؤول")]
        public int? ReceiverId { get; set; }

        [Display(Name = "حالة الحساب")]
        public bool IsActive { get; set; }

        [Display(Name = "موجود في قاعدة البيانات")]
        public bool IsInDatabase { get; set; }

        [Display(Name = "معرف العميل")]
        public int? ClientId { get; set; }

        [Display(Name = "نوع الخدمة")]
        [StringLength(50, ErrorMessage = "نوع الخدمة يجب أن لا يتجاوز 50 حرف")]
        public string? Service { get; set; }

        [Display(Name = "العنوان")]
        [StringLength(50, ErrorMessage = "العنوان يجب أن لا يتجاوز 50 حرف")]
        public string? Address { get; set; }

        [Display(Name = "رقم وطني")]
        [StringLength(20, ErrorMessage = "الرقم الوطني يجب أن لا يتجاوز 20 حرف")]
        public string? SID { get; set; }

        [Display(Name = "حالة الاتصال")]
        [StringLength(50, ErrorMessage = "حالة الاتصال يجب أن لا تتجاوز 50 حرف")]
        public string? ConnectionStatus { get; set; }

        [Display(Name = "تاريخ انتهاء الصلاحية")]
        [DataType(DataType.Date)]
        public DateTime? AccountExpirationDate { get; set; }
    }
}

