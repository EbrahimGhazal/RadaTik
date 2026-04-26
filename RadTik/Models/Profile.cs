using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public enum ProfileType
    {
        [Description("خدمة إنترنت عادية")]
        Internet,

        [Description("خدمة البث التلفزيوني عبر الإنترنت")]
        IPTV,

        [Description("خدمة الصوت عبر بروتوكول الإنترنت")]
        VoIP,

        [Description("باقة تشمل إنترنت + IPTV")]
        Bundle,

        [Description("بروفايل مخصص حسب الاحتياجات")]
        Custom
    }

    public enum SpeedUnit
    {
        [Description("Kbps")]
        Kbps = 0,
        [Description("Mbps")]
        Mbps = 1,
        [Description("Gbps")]
        Gbps = 2
    }

    public enum BillingCycle
    {
        [Description("يتم الفوترة بشكل يومي")]
        Daily,

        [Description("يتم الفوترة بشكل أسبوعي")]
        Weekly,

        [Description("يتم الفوترة بشكل شهري")]
        Monthly,

        [Description("يتم الفوترة كل 3 أشهر")]
        Quarterly,

        [Description("يتم الفوترة كل 6 أشهر")]
        SemiAnnual,

        [Description("يتم الفوترة بشكل سنوي")]
        Annual,

        [Description("دفعة لمرة واحدة")]
        OneTime
    }

    public class Profile
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم البروفايل مطلوب")]
        [Display(Name = "اسم البروفايل")]
        [StringLength(100, ErrorMessage = "اسم البروفايل يجب أن لا يتجاوز 100 حرف")]
        [Description("اسم البروفايل كما سيظهر في النظام وفي MikroTik - يجب أن يكون فريداً")]
        public string Name { get; set; } = null!;

        [Display(Name = "الوصف")]
        [DataType(DataType.MultilineText)]
        [Description("وصف مختصر للبروفايل والخدمات المقدمة")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "نوع البروفايل مطلوب")]
        [Display(Name = "نوع البروفايل")]
        [Description("اختر نوع الخدمة التي يقدمها هذا البروفايل")]
        public ProfileType Type { get; set; }

        [Required(ErrorMessage = "دورة الفاتورة مطلوبة")]
        [Display(Name = "دورة الفاتورة")]
        [Description("الفترة الزمنية للفوترة")]
        public BillingCycle BillingCycle { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Display(Name = "السعر")]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 1000000, ErrorMessage = "السعر يجب أن يكون بين 0 و 1,000,000")]
        [Description("السعر الأساسي قبل الضريبة - استخدم 0 للخدمات المجانية")]
        public decimal Price { get; set; }

        [Display(Name = "ضريبة القيمة المضافة %")]
        [Range(0, 100, ErrorMessage = "النسبة يجب أن تكون بين 0 و 100")]
        [Description("نسبة الضريبة المضافة للسعر - القيمة الافتراضية 15%")]
        public decimal VATPercentage { get; set; } = 15;

        [Display(Name = "السعر بعد الضريبة")]
        [Column(TypeName = "decimal(18,2)")]
        [Description("يتم حسابه تلقائياً بناءً على السعر ونسبة الضريبة")]
        public decimal PriceWithVAT => Price * (1 + VATPercentage / 100);

        [Required(ErrorMessage = "سرعة التنزيل مطلوبة")]
        [Display(Name = "سرعة التنزيل")]
        [Range(1, int.MaxValue, ErrorMessage = "السرعة يجب أن تكون رقماً صحيحاً أكبر من صفر")]
        [Description("السرعة كرقم صحيح")]
        public int DownloadSpeed { get; set; }

        [Display(Name = "وحدة سرعة التنزيل")]
        public SpeedUnit DownloadSpeedUnit { get; set; } = SpeedUnit.Mbps;

        [Display(Name = "سرعة الرفع")]
        [Range(1, int.MaxValue, ErrorMessage = "السرعة يجب أن تكون رقماً صحيحاً أكبر من صفر")]
        [Description("السرعة كرقم صحيح - اتركه فارغاً لاستخدام نفس سرعة التنزيل")]
        public int? UploadSpeed { get; set; }

        [Display(Name = "وحدة سرعة الرفع")]
        public SpeedUnit? UploadSpeedUnit { get; set; }

        [NotMapped]
        [Display(Name = "سرعة التنزيل بالعرض")]
        public string DownloadSpeedDisplay => $"{DownloadSpeed} {DownloadSpeedUnit}";

        [NotMapped]
        [Display(Name = "سرعة الرفع بالعرض")]
        public string? UploadSpeedDisplay => UploadSpeed.HasValue
            ? $"{UploadSpeed} {(UploadSpeedUnit ?? DownloadSpeedUnit)}"
            : null;

        /// <summary>إرجاع سرعة التنزيل بالميجابت للقارنة</summary>
        [NotMapped]
        public decimal DownloadSpeedMbps => DownloadSpeedUnit switch
        {
            SpeedUnit.Kbps => DownloadSpeed / 1000m,
            SpeedUnit.Mbps => DownloadSpeed,
            SpeedUnit.Gbps => DownloadSpeed * 1000,
            _ => DownloadSpeed
        };

        /// <summary>إرجاع سرعة الرفع بالميجابت للقارنة</summary>
        [NotMapped]
        public decimal? UploadSpeedMbps => UploadSpeed.HasValue
            ? (UploadSpeedUnit ?? DownloadSpeedUnit) switch
            {
                SpeedUnit.Kbps => UploadSpeed.Value / 1000m,
                SpeedUnit.Mbps => UploadSpeed.Value,
                SpeedUnit.Gbps => UploadSpeed.Value * 1000,
                _ => UploadSpeed.Value
            }
            : null;

        [Display(Name = "حد البيانات (GB)")]
        [Range(0, double.MaxValue, ErrorMessage = "الحد يجب أن يكون أكبر من 0")]
        [Description("حد استهلاك البيانات بالجيجابايت - 0 يعني غير محدود")]
        public decimal? DataLimit { get; set; }

        [Display(Name = "حد الوقت (ساعة)")]
        [Range(0, 744, ErrorMessage = "الحد يجب أن يكون بين 0 و 744 ساعة")]
        [Description("حد الوقت بالساعات (744 ساعة = 31 يوم) - 0 يعني غير محدود")]
        public int? TimeLimit { get; set; }

        [Display(Name = "عدد أجهزة IPTV")]
        [Range(1, 10, ErrorMessage = "العدد يجب أن يكون بين 1 و 10")]
        [Description("عدد الأجهزة المسموحة لمشاهدة IPTV في نفس الوقت")]
        public int? IPTVDevices { get; set; }

        [Display(Name = "محدد البيانات")]
        [Description("تفعيل تحديد استهلاك البيانات - سيتم قطع الخدمة عند تجاوز الحد")]
        public bool IsDataCapped { get; set; }

        [Display(Name = "محدد الوقت")]
        [Description("تفعيل تحديد وقت الاستخدام - سيتم قطع الخدمة عند تجاوز الحد")]
        public bool IsTimeCapped { get; set; }

        [Display(Name = "الحد الأقصى للمستخدمين")]
        [Range(1, 1000, ErrorMessage = "العدد يجب أن يكون بين 1 و 1000")]
        [Description("الحد الأقصى لعدد المستخدمين الذين يمكنهم استخدام هذا البروفايل في نفس الوقت")]
        public int MaxUsers { get; set; } = 1;

        [Display(Name = "عدد الأجهزة الأدنى")]
        [Range(0, 100, ErrorMessage = "العدد يجب أن يكون بين 0 و 100")]
        [Description("الحد الأدنى للأجهزة المتصلة في نفس الوقت - يساعد المستخدم في اختيار الخطة المناسبة")]
        public int MinDevices { get; set; } = 1;

        [Display(Name = "عدد الأجهزة الأقصى")]
        [Range(1, 100, ErrorMessage = "العدد يجب أن يكون بين 1 و 100")]
        [Description("الحد الأقصى للأجهزة المتصلة في نفس الوقت - يساعد المستخدم في اختيار الخطة المناسبة")]
        public int MaxDevices { get; set; } = 1;

        [Display(Name = "البورتات المسموحة")]
        [Description("قائمة البورتات المسموح بها (مفصولة بفواصل) مثل: 80,443,53 - اتركه فارغاً للسماح بجميع البورتات")]
        public string? AllowedPorts { get; set; }

        [Display(Name = "العناوين المسموحة")]
        [Description("قائمة العناوين المسموح بها (مفصولة بفواصل) - اتركه فارغاً للسماح بجميع العناوين")]
        public string? AllowedAddresses { get; set; }

        [Display(Name = "الميزات")]
        [Description("الميزات الإضافية للبروفايل مثل: VPN مخصص، دعم فني متميز، إلخ")]
        public string? Features { get; set; }

        [Required]
        [Display(Name = "مفعل")]
        [Description("تفعيل البروفايل للاستخدام - سيظهر في قائمة البروفايلات المتاحة")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "للعملاء الجدد")]
        [Description("هل هذا البروفايل متاح للعملاء الجدد؟")]
        public bool IsForNewClients { get; set; } = true;

        [Display(Name = "ترتيب العرض")]
        [Description("ترتيب ظهور البروفايل في القوائم - الأقل يعني الأعلى")]
        public int DisplayOrder { get; set; }

        [Display(Name = "تمت المزامنة مع MikroTik")]
        [Description("هل تمت مزامنة البروفايل مع خادم MikroTik؟")]
        public bool IsSyncedWithMikroTik { get; set; }

        [Display(Name = "معرف البروفايل في MikroTik")]
        [Description("المعرف الفريد للبروفايل في خادم MikroTik")]
        public string? MikroTikProfileId { get; set; }

        [Required(ErrorMessage = "خادم MikroTik مطلوب")]
        [Display(Name = "خادم MikroTik المرتبط")]
        [Description("خادم MikroTik الذي سيتم تخزين البروفايل عليه - كل بروفايل خاص بخادم واحد")]
        public int MikroTikServerId { get; set; }

        [ForeignKey("MikroTikServerId")]
        public virtual MikroTikServer? MikroTikServer { get; set; }

        // علاقة مع Network
        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey("NetworkId")]
        public virtual Network? Network { get; set; }

        [Display(Name = "Local Address في MikroTik")]
        [Description("عنوان IP المحلي الذي سيتم تعيينه للبروفايل في MikroTik")]
        public string? MikroTikLocalAddress { get; set; }

        [Display(Name = "Remote Address في MikroTik")]
        [Description("عنوان IP البعيد الذي سيتم تعيينه للبروفايل في MikroTik")]
        public string? MikroTikRemoteAddress { get; set; }

        [Display(Name = "Rate Limit في MikroTik")]
        [Description("حد السرعة في MikroTik (مثال: 10M/10M للتنزيل/الرفع)")]
        public string? MikroTikRateLimit { get; set; }

        [Display(Name = "Only One في MikroTik")]
        [Description("السماح باتصال واحد فقط من نفس المستخدم في MikroTik")]
        public bool MikroTikOnlyOne { get; set; } = true;

        [Display(Name = "Service في MikroTik")]
        [Description("نوع الخدمة في MikroTik (pppoe, pptp, l2tp)")]
        public string? MikroTikService { get; set; } = "pppoe";

        [Display(Name = "تاريخ الإنشاء")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "تاريخ التحديث")]
        public DateTime UpdatedDate { get; set; } = DateTime.Now;

        [Display(Name = "آخر مزامنة")]
        public DateTime? LastSyncDate { get; set; }

        // Navigation Properties
        public virtual ICollection<Client> Clients { get; set; } = new List<Client>();
        public virtual ICollection<ProfilePriceHistory> ProfilePriceHistories { get; set; } = new List<ProfilePriceHistory>();
    }

    public class ProfilePriceHistory
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int ProfileId { get; set; }

        [Required]
        [Display(Name = "السعر القديم")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OldPrice { get; set; }

        [Required]
        [Display(Name = "السعر الجديد")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NewPrice { get; set; }

        [Display(Name = "نسبة الضريبة القديمة")]
        public decimal OldVATPercentage { get; set; }

        [Display(Name = "نسبة الضريبة الجديدة")]
        public decimal NewVATPercentage { get; set; }

        [Required]
        [Display(Name = "سبب التغيير")]
        [StringLength(200)]
        public string ChangeReason { get; set; } = null!;

        [Display(Name = "تاريخ التغيير")]
        public DateTime ChangeDate { get; set; } = DateTime.Now;

        [Display(Name = "تم بواسطة")]
        public string? ChangedBy { get; set; }

        // العلاقات
        [ForeignKey("ProfileId")]
        public virtual Profile Profile { get; set; } = null!;
    }
}
