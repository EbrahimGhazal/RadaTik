using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// نوع طلب الانضمام
    /// </summary>
    public enum JoinRequestType
    {
        [Display(Name = "عميل")]
        Client = 1,
        
        [Display(Name = "موظف")]
        Employee = 2,
        
        [Display(Name = "مدير شبكة")]
        NetworkAdministrator = 3,

        [Display(Name = "نقطة تحصيل")]
        CollectionPoint = 4
    }

    /// <summary>
    /// حالة طلب الانضمام
    /// </summary>
    public enum JoinRequestStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 1,
        
        [Display(Name = "قيد المراجعة")]
        UnderReview = 2,
        
        [Display(Name = "مقبول")]
        Approved = 3,
        
        [Display(Name = "مرفوض")]
        Rejected = 4
    }

    /// <summary>
    /// نموذج طلب الانضمام للشركة كعميل أو موظف
    /// </summary>
    public class JoinRequest
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "نوع الطلب مطلوب")]
        [Display(Name = "نوع الطلب")]
        public JoinRequestType RequestType { get; set; }

        [Required(ErrorMessage = "الاسم الكامل مطلوب")]
        [StringLength(100)]
        [Display(Name = "الاسم الكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Phone(ErrorMessage = "رقم الهاتف غير صحيح")]
        [StringLength(20)]
        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        // للعملاء
        [Display(Name = "رقم الهوية")]
        [StringLength(20)]
        public string? NationalId { get; set; }

        [Display(Name = "الباقة المطلوبة")]
        public int? RequestedProfileId { get; set; }

        [ForeignKey("RequestedProfileId")]
        public virtual Profile? RequestedProfile { get; set; }

        // للموظفين
        [Display(Name = "المؤهل العلمي")]
        [StringLength(100)]
        public string? Qualification { get; set; }

        [Display(Name = "الخبرات السابقة")]
        [StringLength(500)]
        public string? Experience { get; set; }

        [Display(Name = "الوظيفة المطلوبة")]
        [StringLength(100)]
        public string? DesiredPosition { get; set; }

        // معلومات إضافية
        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "كلمة المرور المطلوبة (مشفرة)")]
        [StringLength(512)]
        public string? RequestedPassword { get; set; }

        [Display(Name = "حالة الطلب")]
        public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;

        [Display(Name = "ملاحظات الإدارة")]
        [StringLength(500)]
        public string? AdminNotes { get; set; }

        [Display(Name = "تاريخ التقديم")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ آخر تحديث")]
        public DateTime? UpdatedDate { get; set; }

        [Display(Name = "معرف الموظف المعالج")]
        public string? ProcessedByUserId { get; set; }

        [ForeignKey("ProcessedByUserId")]
        public virtual ApplicationUser? ProcessedByUser { get; set; }

        [Display(Name = "تاريخ المعالجة")]
        public DateTime? ProcessedDate { get; set; }
    }

    /// <summary>
    /// نموذج طلب استعادة كلمة المرور
    /// </summary>
    public class PasswordResetRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Display(Name = "معرف المستخدم")]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "طريقة الاستعادة")]
        public PasswordResetMethod ResetMethod { get; set; }

        [Display(Name = "رمز التحقق")]
        [StringLength(6)]
        public string? VerificationCode { get; set; }

        [Display(Name = "تاريخ انتهاء الرمز")]
        public DateTime? CodeExpiryDate { get; set; }

        [Display(Name = "الحالة")]
        public PasswordResetStatus Status { get; set; } = PasswordResetStatus.Pending;

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "تاريخ الطلب")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ المعالجة")]
        public DateTime? ProcessedDate { get; set; }

        [Display(Name = "معرف المعالج")]
        public string? ProcessedByUserId { get; set; }

        [ForeignKey("ProcessedByUserId")]
        public virtual ApplicationUser? ProcessedByUser { get; set; }
    }

    public enum PasswordResetMethod
    {
        [Display(Name = "عبر البريد الإلكتروني")]
        Email = 1,

        [Display(Name = "طلب لمدير النظام")]
        AdminRequest = 2
    }

    public enum PasswordResetStatus
    {
        [Display(Name = "قيد الانتظار")]
        Pending = 1,

        [Display(Name = "تم الإرسال")]
        CodeSent = 2,

        [Display(Name = "تم التحقق")]
        Verified = 3,

        [Display(Name = "مكتمل")]
        Completed = 4,

        [Display(Name = "ملغي")]
        Cancelled = 5,

        [Display(Name = "منتهي الصلاحية")]
        Expired = 6
    }
}
