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
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public DateTime PrintedAt { get; set; } = DateTime.Now;
        public decimal TotalWithoutTax { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalWithTax { get; set; }
        public int ResultCount { get; set; }
        public List<PaymentTransaction> Transactions { get; set; } = new();
        public List<CollectionPointDetailsPrintRow> PrintRows { get; set; } = new();

        public string PeriodLabel => CollectionPointPeriodLabel.Format(From, To);
    }

    public class CollectionPointDetailsPrintRow
    {
        public int Sequence { get; set; }
        public string FullName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string ProfileSpeed { get; set; } = "";
        public string OperationTypeLabel { get; set; } = "";
        public decimal AmountPaid { get; set; }
        public DateTime PaymentDate { get; set; }
    }

    /// <summary>تقرير تسديدات مشتركي الشركة عبر نقاط التحصيل.</summary>
    public class CollectionPointPaymentsReportViewModel
    {
        public DateTime? From { get; set; }
        public DateTime? To { get; set; }
        public string? CollectionPointUserId { get; set; }
        public DateTime PrintedAt { get; set; } = DateTime.Now;
        public decimal TotalWithoutTax { get; set; }
        public decimal TaxAmount { get; set; }
        public decimal TotalWithTax { get; set; }
        public int ResultCount { get; set; }
        public List<CollectionPointPaymentReportRow> Rows { get; set; } = new();
        public List<CollectionPointFilterOption> CollectionPointOptions { get; set; } = new();

        public string PeriodLabel => CollectionPointPeriodLabel.Format(From, To);
    }

    public class CollectionPointPaymentReportRow
    {
        public int Sequence { get; set; }
        public DateTime PaymentDate { get; set; }
        public string SubscriberName { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string ProfileSpeed { get; set; } = "";
        public string CollectionPointName { get; set; } = "";
        public int? CollectionPointAccountId { get; set; }
        public string OperationTypeLabel { get; set; } = "";
        public string OperationType { get; set; } = "";
        public decimal AmountPaid { get; set; }
    }

    public class CollectionPointFilterOption
    {
        public string UserId { get; set; } = "";
        public string DisplayName { get; set; } = "";
    }

    internal static class CollectionPointPeriodLabel
    {
        public static string Format(DateTime? from, DateTime? to)
        {
            if (from.HasValue && to.HasValue)
            {
                return $"من {from:yyyy/MM/dd} إلى {to:yyyy/MM/dd}";
            }

            if (from.HasValue)
            {
                return $"من {from:yyyy/MM/dd}";
            }

            if (to.HasValue)
            {
                return $"حتى {to:yyyy/MM/dd}";
            }

            return "كل العمليات";
        }
    }
}

