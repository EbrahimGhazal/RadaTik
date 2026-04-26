using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// حالات طلب الصيانة
    /// </summary>
    public enum MaintenanceRequestStatus
    {
        [Description("في انتظار المراجعة")]
        Pending,

        [Description("تم قبول الطلب")]
        Accepted,

        [Description("قيد التنفيذ")]
        InProgress,

        [Description("تم الإنجاز")]
        Completed,

        [Description("مرفوض")]
        Rejected,

        [Description("ملغي")]
        Cancelled
    }

    /// <summary>
    /// أنواع طلبات الصيانة
    /// </summary>
    public enum MaintenanceType
    {
        [Description("انقطاع في الخدمة")]
        ServiceOutage,

        [Description("بطء في الاتصال")]
        SlowConnection,

        [Description("مشكلة في الراوتر")]
        RouterIssue,

        [Description("مشكلة في الكابلات")]
        CableIssue,

        [Description("مشكلة في التكوين")]
        ConfigurationIssue,

        [Description("طلب زيارة فنية")]
        TechnicianVisit,

        [Description("أخرى")]
        Other,

        [Description("تغيير لاقط")]
        ReceiverReplacement,

        [Description("تغيير كبل")]
        CableReplacement,

        [Description("تغيير موصلات RG45")]
        Rg45ConnectorReplacement,

        [Description("تغيير اعدادت راوتر")]
        RouterSettingsChange,

        [Description("تغيير كلمة سر راوتر")]
        RouterPasswordChange,

        [Description("تغيير POE")]
    PoeChange,

    [Description("لا يوجد إنترنت")]
    NoInternet,

    [Description("الراوتر لا يعمل")]
    RouterNotWorking,

    [Description("الراوتر يعمل لكن ليد الإنترنت لا يعمل")]
    RouterInternetLedOff,

    [Description("الراوتر يعمل لكن ليد الإنترنت وليد WAN لا يعملان")]
    RouterInternetAndWanLedsOff,

    [Description("كشف دوري")]
    PeriodicInspection,

    [Description("تغيير راوتر")]
    RouterReplacement,

    [Description("تغيير سويتش")]
    SwitchReplacement
    }

public static class MaintenanceCatalog
{
    public static readonly MaintenanceType[] ProblemTypes =
    [
        MaintenanceType.NoInternet,
        MaintenanceType.SlowConnection,
        MaintenanceType.PeriodicInspection,
        MaintenanceType.RouterNotWorking,
        MaintenanceType.RouterInternetLedOff,
        MaintenanceType.RouterInternetAndWanLedsOff,
        MaintenanceType.Other
    ];

    public static readonly MaintenanceType[] SolutionTypes =
    [
        MaintenanceType.CableReplacement,
        MaintenanceType.ReceiverReplacement,
        MaintenanceType.PoeChange,
        MaintenanceType.Rg45ConnectorReplacement,
        MaintenanceType.RouterSettingsChange,
        MaintenanceType.RouterReplacement,
        MaintenanceType.SwitchReplacement
    ];

    private static readonly MaintenanceType[] DisplayOrder =
    [
        ..ProblemTypes,
        ..SolutionTypes,
        MaintenanceType.ServiceOutage,
        MaintenanceType.RouterIssue,
        MaintenanceType.CableIssue,
        MaintenanceType.ConfigurationIssue,
        MaintenanceType.TechnicianVisit,
        MaintenanceType.RouterPasswordChange
    ];

    public static bool IsSolutionType(MaintenanceType type) => SolutionTypes.Contains(type);

    public static int GetOrder(MaintenanceType type)
    {
        var index = Array.IndexOf(DisplayOrder, type);
        return index >= 0 ? index : int.MaxValue;
    }

    public static string GetDisplayName(MaintenanceType type) => type switch
    {
        MaintenanceType.NoInternet => "لا يوجد إنترنت",
        MaintenanceType.SlowConnection => "بطء في النت",
        MaintenanceType.PeriodicInspection => "كشف دوري",
        MaintenanceType.RouterNotWorking => "الراوتر لا يعمل",
        MaintenanceType.RouterInternetLedOff => "الراوتر يعمل لكن ليد الإنترنت لا يعمل",
        MaintenanceType.RouterInternetAndWanLedsOff => "الراوتر يعمل لكن ليد الإنترنت وليد WAN لا يعملان",
        MaintenanceType.CableReplacement => "تغيير كبل",
        MaintenanceType.ReceiverReplacement => "تغيير لاقط",
        MaintenanceType.PoeChange => "تغيير POE",
        MaintenanceType.Rg45ConnectorReplacement => "تغيير RG",
        MaintenanceType.RouterSettingsChange => "تغيير إعدادات راوتر",
        MaintenanceType.RouterReplacement => "تغيير راوتر",
        MaintenanceType.SwitchReplacement => "تغيير سويتش",
        MaintenanceType.Other => "أخرى",
        MaintenanceType.ServiceOutage => "انقطاع في الخدمة",
        MaintenanceType.RouterIssue => "مشكلة في الراوتر",
        MaintenanceType.CableIssue => "مشكلة في الكابلات",
        MaintenanceType.ConfigurationIssue => "مشكلة في التكوين",
        MaintenanceType.TechnicianVisit => "طلب زيارة فنية",
        MaintenanceType.RouterPasswordChange => "تغيير كلمة سر راوتر",
        _ => type.ToString()
    };

    public static string GetIcon(MaintenanceType type) => type switch
    {
        MaintenanceType.NoInternet => "fa-globe",
        MaintenanceType.SlowConnection => "fa-tachometer-alt",
        MaintenanceType.PeriodicInspection => "fa-clipboard-check",
        MaintenanceType.RouterNotWorking => "fa-power-off",
        MaintenanceType.RouterInternetLedOff => "fa-lightbulb",
        MaintenanceType.RouterInternetAndWanLedsOff => "fa-network-wired",
        MaintenanceType.CableReplacement => "fa-ethernet",
        MaintenanceType.ReceiverReplacement => "fa-satellite-dish",
        MaintenanceType.PoeChange => "fa-plug",
        MaintenanceType.Rg45ConnectorReplacement => "fa-plug",
        MaintenanceType.RouterSettingsChange => "fa-sliders-h",
        MaintenanceType.RouterReplacement => "fa-rotate",
        MaintenanceType.SwitchReplacement => "fa-diagram-project",
        _ => "fa-tools"
    };
}

    /// <summary>
    /// أولوية الطلب
    /// </summary>
    public enum RequestPriority
    {
        [Description("منخفضة")]
        Low,

        [Description("عادية")]
        Normal,

        [Description("عالية")]
        High,

        [Description("عاجلة")]
        Urgent
    }

    /// <summary>
    /// نموذج طلب الصيانة
    /// </summary>
    public class MaintenanceRequest
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "العميل مطلوب")]
        [Display(Name = "العميل")]
        public int ClientId { get; set; }

        [Required(ErrorMessage = "نوع المشكلة مطلوب")]
        [Display(Name = "نوع المشكلة")]
        public MaintenanceType Type { get; set; }

        [Required(ErrorMessage = "وصف المشكلة مطلوب")]
        [Display(Name = "وصف المشكلة")]
        [StringLength(1000, ErrorMessage = "الوصف يجب أن لا يتجاوز 1000 حرف")]
        [DataType(DataType.MultilineText)]
        public string Description { get; set; } = null!;

        [Display(Name = "الأولوية")]
        public RequestPriority Priority { get; set; } = RequestPriority.Normal;

        [Display(Name = "حالة الطلب")]
        public MaintenanceRequestStatus Status { get; set; } = MaintenanceRequestStatus.Pending;

        [Display(Name = "تاريخ تقديم الطلب")]
        public DateTime RequestDate { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ القبول")]
        public DateTime? AcceptedDate { get; set; }

        [Display(Name = "تاريخ الإنجاز")]
        public DateTime? CompletedDate { get; set; }

        [Display(Name = "ملاحظات الفني")]
        [StringLength(1000)]
        [DataType(DataType.MultilineText)]
        public string? TechnicianNotes { get; set; }

        [Display(Name = "سبب الرفض")]
        [StringLength(500)]
        public string? RejectionReason { get; set; }

        [Display(Name = "الموظف المسؤول")]
        public string? AssignedToId { get; set; }

        [Display(Name = "معالج بواسطة")]
        public string? ProcessedById { get; set; }

        [Display(Name = "رقم الهاتف للتواصل")]
        [StringLength(20)]
        public string? ContactPhone { get; set; }

        [Display(Name = "أفضل وقت للتواصل")]
        [StringLength(100)]
        public string? PreferredContactTime { get; set; }

        [Display(Name = "العنوان")]
        [StringLength(500)]
        public string? Address { get; set; }

        /// <summary>
        /// الموعد المحدد لزيارة الصيانة (يظهر في لوحة الموظف ليوم الموعد).
        /// </summary>
        [Display(Name = "موعد الزيارة")]
        public DateTime? ScheduledVisitDate { get; set; }

        // Navigation Properties
        [ForeignKey("ClientId")]
        public virtual Client? Client { get; set; }

        [ForeignKey("AssignedToId")]
        public virtual ApplicationUser? AssignedTo { get; set; }

        [ForeignKey("ProcessedById")]
        public virtual ApplicationUser? ProcessedBy { get; set; }
    }
}
