using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models
{
    /// <summary>
    /// حالات طلب تغيير السرعة
    /// </summary>
    public enum SpeedChangeRequestStatus
    {
        [Description("في انتظار المراجعة")]
        Pending,

        [Description("تم الموافقة")]
        Approved,

        [Description("مرفوض")]
        Rejected,

        [Description("تم التنفيذ")]
        Implemented,

        [Description("ملغي")]
        Cancelled
    }

    /// <summary>
    /// نموذج طلب تغيير السرعة (تغيير الباقة)
    /// </summary>
    public class SpeedChangeRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "العميل مطلوب")]
        [Display(Name = "العميل")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "الباقة الحالية مطلوبة")]
        [Display(Name = "الباقة الحالية")]
        public int CurrentProfileId { get; set; }

        [Required(ErrorMessage = "الباقة المطلوبة مطلوبة")]
        [Display(Name = "الباقة المطلوبة")]
        public int RequestedProfileId { get; set; }

        [Display(Name = "سبب التغيير")]
        [StringLength(500)]
        [DataType(DataType.MultilineText)]
        public string? Reason { get; set; }

        [Display(Name = "حالة الطلب")]
        public SpeedChangeRequestStatus Status { get; set; } = SpeedChangeRequestStatus.Pending;

        [Display(Name = "تاريخ تقديم الطلب")]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ المعالجة")]
        public DateTime? ProcessedDate { get; set; }

        [Display(Name = "تاريخ التنفيذ")]
        public DateTime? ImplementedDate { get; set; }

        [Display(Name = "سبب الرفض")]
        [StringLength(500)]
        public string? RejectionReason { get; set; }

        [Display(Name = "ملاحظات المدير")]
        [StringLength(1000)]
        [DataType(DataType.MultilineText)]
        public string? AdminNotes { get; set; }

        [Display(Name = "معالج بواسطة")]
        public string? ProcessedById { get; set; }

        [Display(Name = "تم التنفيذ بواسطة")]
        public string? ImplementedById { get; set; }

        [Display(Name = "فرق السعر")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal? PriceDifference { get; set; }

        [Display(Name = "تم دفع الفرق")]
        public bool IsPriceDifferencePaid { get; set; }

        // Navigation Properties
        [ForeignKey("ClientId")]
        public virtual Client? Client { get; set; }

        [ForeignKey("CurrentProfileId")]
        public virtual Profile? CurrentProfile { get; set; }

        [ForeignKey("RequestedProfileId")]
        public virtual Profile? RequestedProfile { get; set; }

        [ForeignKey("ProcessedById")]
        public virtual ApplicationUser? ProcessedBy { get; set; }

        [ForeignKey("ImplementedById")]
        public virtual ApplicationUser? ImplementedBy { get; set; }
    }
}
