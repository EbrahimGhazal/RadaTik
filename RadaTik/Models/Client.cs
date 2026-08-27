using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    public class Client
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // البيانات الأساسية من الملف القديم
        [Required(ErrorMessage = "الاسم الثلاثي مطلوب")]
        [Display(Name = "الاسم الثلاثي")]
        [StringLength(100, ErrorMessage = "الاسم يجب أن لا يتجاوز 100 حرف")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "الرقم الوطني مطلوب")]
        [Display(Name = "الرقم الوطني")]
        [StringLength(20, ErrorMessage = "الرقم يجب أن لا يتجاوز 20 حرف")]
        [RegularExpression(@"^\d+$", ErrorMessage = "الرقم الوطني يجب أن يحتوي على أرقام فقط")]
        public string? SID { get; set; }

        [Required(ErrorMessage = "اسم المستخدم مطلوب")]
        [Display(Name = "اسم المستخدم")]
        [StringLength(100, ErrorMessage = "اسم المستخدم يجب أن لا يتجاوز 100 حرف")]
        public string? UserName { get; set; }

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [Display(Name = "كلمة المرور")]
        [StringLength(100, ErrorMessage = "كلمة المرور يجب أن لا تتجاوز 100 حرف")]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        // تغيير: الآن سنستخدم ProfileId بدلاً من ProfileName كنص
        [Required(ErrorMessage = "البروفايل مطلوب")]
        [Display(Name = "البروفايل")]
        public int ProfileId { get; set; }

        // الاحتفاظ باسم البروفايل للتوافق مع البيانات القديمة
        [Display(Name = "اسم البروفايل (قديم)")]
        [StringLength(100, ErrorMessage = "اسم البروفايل يجب أن لا يتجاوز 100 حرف")]
        public string? ProfileName { get; set; }

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم الهاتف")]
        [StringLength(15, ErrorMessage = "رقم الهاتف يجب أن لا يتجاوز 15 رقماً")]
        [RegularExpression(@"^[\d\s\-\+]+$", ErrorMessage = "رقم الهاتف يجب أن يحتوي على أرقام فقط")]
        public string? PhoneNumber { get; set; }

        /// <summary>معرّف محادثة تلغرام لإرسال تذكيرات التجديد (اختياري).</summary>
        [Display(Name = "تلغرام Chat Id")]
        [StringLength(64)]
        public string? TelegramChatId { get; set; }

        [Display(Name = "مكان السكن")]
        [StringLength(500, ErrorMessage = "مكان السكن يجب أن لا يتجاوز 500 حرف")]
        public string? ResidenceAddress { get; set; }

        [Display(Name = "العمل الوظيفي")]
        [StringLength(100, ErrorMessage = "العمل الوظيفي يجب أن لا يتجاوز 100 حرف")]
        public string? Occupation { get; set; }

        [Display(Name = "مكان العمل")]
        [StringLength(200, ErrorMessage = "مكان العمل يجب أن لا يتجاوز 200 حرف")]
        public string? Workplace { get; set; }

        [Display(Name = "خط العرض")]
        public double? Latitude { get; set; }

        [Display(Name = "خط الطول")]
        public double? Longitude { get; set; }

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;

        // Foreign Key for Receiver - جعله nullable
        [Display(Name = "المستقبل")]
        public int? ReceiverId { get; set; }

        // البيانات من الملف الجديد (للاتصال بالمايكروتك)
        [Display(Name = "الخدمة")]
        public string? Service { get; set; }

        [Display(Name = "العنوان IP")]
        public string? Address { get; set; }

        [Display(Name = "وقت التشغيل")]
        public string? Uptime { get; set; }

        [Display(Name = "حالة الاتصال")]
        public string? ConnectionStatus { get; set; }

        [Display(Name = "الماك ادرس")]
        public string? MacAddress { get; set; }

        [Display(Name = "خادم المايكروتك")]
        public int? MikroTikServerId { get; set; }

        /// <summary>
        /// الحساب موجود بنفس اسم المستخدم على سيرفر MikroTik آخر ضمن نفس الشبكة.
        /// يُستورد كسجل مستقل ويُعلَّم كمكرر لتمييزه في الواجهة.
        /// </summary>
        [Display(Name = "مكرر عبر السيرفرات")]
        public bool IsCrossServerDuplicate { get; set; }

        [Display(Name = "مشترك مميز (VIP)")]
        public bool IsVip { get; set; }

        [Display(Name = "ملاحظة التمييز")]
        [StringLength(200, ErrorMessage = "ملاحظة التمييز يجب أن لا تتجاوز 200 حرف")]
        public string? VipNote { get; set; }

        [Display(Name = "تاريخ التعليم كـ VIP")]
        public DateTime? VipSince { get; set; }

        [Display(Name = "ميزة المميز")]
        public ClientVipBenefitKind VipBenefitKind { get; set; }

        [Display(Name = "نسبة الحسم (%)")]
        [Range(0, 100, ErrorMessage = "نسبة الحسم يجب أن تكون بين 0 و 100")]
        public decimal VipDiscountPercent { get; set; }

        [Display(Name = "آخر تحديث")]
        public DateTime LastUpdated { get; set; } = DateTime.Now;

        // معلومات الفاتورة
        [Display(Name = "تاريخ بداية الخدمة")]
        [DataType(DataType.Date)]
        public DateTime? ServiceStartDate { get; set; }

        /// <summary>
        /// حقل قديم (Legacy): لم يعد يُستخدم لتحديد مواعيد التركيب.
        /// الموعد الفعلي المعتمد حالياً هو CreatedDate.
        /// تم الإبقاء عليه فقط للتوافق مع البيانات القديمة.
        /// </summary>
        [Display(Name = "موعد التركيب")]
        [DataType(DataType.DateTime)]
        public DateTime? ScheduledInstallationDate { get; set; }

        [Display(Name = "تاريخ نهاية الخدمة")]
        [DataType(DataType.Date)]
        public DateTime? ServiceEndDate { get; set; }

        [Display(Name = "تاريخ الفاتورة القادمة")]
        [DataType(DataType.Date)]
        public DateTime? NextBillingDate { get; set; }

        [Display(Name = "الرصيد")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        /// <summary>عملة رصيد الحساب والاشتراك (الافتراضي ل.س.ج).</summary>
        [Display(Name = "عملة الحساب")]
        public PricingCurrency AccountCurrency { get; set; } = PricingCurrency.SYP_New;

        // تاريخ انتهاء صلاحية الحساب PPPoE
        [Display(Name = "تاريخ انتهاء الصلاحية")]
        [DataType(DataType.Date)]
        public DateTime? AccountExpirationDate { get; set; }

        [Display(Name = "تاريخ آخر تجديد")]
        [DataType(DataType.Date)]
        public DateTime? LastRenewalDate { get; set; }

        [Display(Name = "مصدر الكهرباء")]
        [StringLength(100, ErrorMessage = "مصدر الكهرباء يجب أن لا يتجاوز 100 حرف")]
        public string? PowerSource { get; set; }

        [Display(Name = "البناء")]
        [StringLength(150, ErrorMessage = "البناء يجب أن لا يتجاوز 150 حرف")]
        public string? Building { get; set; }

        [Display(Name = "الطابق")]
        [StringLength(50, ErrorMessage = "الطابق يجب أن لا يتجاوز 50 حرف")]
        public string? Floor { get; set; }

        // Navigation Properties
        [ForeignKey("ReceiverId")]
        [ValidateNever]
        public virtual Receiver? Receiver { get; set; }

        [ForeignKey("MikroTikServerId")]
        [ValidateNever]
        public virtual MikroTikServer? MikroTikServer { get; set; }

        // العلاقة الجديدة مع البروفايل
        [ForeignKey("ProfileId")]
        [ValidateNever]
        [Display(Name = "البروفايل")]
        public virtual Profile? Profile { get; set; }

        // علاقة مع Network
        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey("NetworkId")]
        public virtual Network? Network { get; set; }

        // دالة لمعرفة إذا كان العميل موجودًا في قاعدة البيانات
        [NotMapped]
        public bool IsInDatabase => Id > 0;

        // خاصية محسوبة للحصول على اسم البروفايل (للتوافق مع الكود القديم)
        [NotMapped]
        [Display(Name = "البروفايل")]
        public string? ProfileDisplayName => Profile?.Name ?? ProfileName;
    }
}
