using RadaTik.Models;
using System.ComponentModel.DataAnnotations;

namespace RadaTik.ViewModels.CollectionPoints
{
    public class CreateCollectionPointViewModel
    {
        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صالح")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "كلمة المرور يجب أن تكون على الأقل 6 أحرف")]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "تأكيد كلمة المرور")]
        [Compare("Password", ErrorMessage = "كلمة المرور وتأكيدها غير متطابقتان")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم الثلاثي مطلوب")]
        [Display(Name = "الاسم الثلاثي")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "رقم الجوال")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [Display(Name = "خط العرض")]
        public double? Latitude { get; set; }

        [Display(Name = "خط الطول")]
        public double? Longitude { get; set; }

        [Display(Name = "الموقع على الخريطة")]
        public string? MapLocation { get; set; }

        [Display(Name = "الرصيد الابتدائي")]
        [DataType(DataType.Currency)]
        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "الرصيد الابتدائي يجب أن يكون صفراً أو قيمة موجبة")]
        public decimal InitialBalance { get; set; }
    }

    public class EditCollectionPointViewModel
    {
        public int Id { get; set; }

        public string UserId { get; set; } = string.Empty;

        [Display(Name = "اسم نقطة التحصيل")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "الرصيد الحالي")]
        [DataType(DataType.Currency)]
        public decimal CurrentBalance { get; set; }

        [Display(Name = "الرصيد الجديد")]
        [DataType(DataType.Currency)]
        [Required(ErrorMessage = "الرجاء إدخال الرصيد الجديد")]
        [Range(typeof(decimal), "0", "999999999999", ErrorMessage = "الرصيد الجديد يجب أن يكون صفراً أو قيمة موجبة")]
        public decimal NewBalance { get; set; }
    }

    public class CollectionPointDetailsViewModel
    {
        public int AccountId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public List<PaymentTransaction> Transactions { get; set; } = new();
    }
}

