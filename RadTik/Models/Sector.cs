using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
using System.Linq;

namespace RadTik.Models
{
    public class Sector
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المرسل مطلوب")]
        [Display(Name = "اسم المرسل")]
        [StringLength(100, ErrorMessage = "الاسم يجب أن لا يتجاوز 100 حرف")]
        public string? Name { get; set; }

        [Required(ErrorMessage = "خط العرض مطلوب")]
        [Display(Name = "خط العرض")]
        [Range(-90, 90, ErrorMessage = "خط العرض يجب أن يكون بين -90 و 90")]
        public double Latitude { get; set; }

        [Required(ErrorMessage = "خط الطول مطلوب")]
        [Display(Name = "خط الطول")]
        [Range(-180, 180, ErrorMessage = "خط الطول يجب أن يكون بين -180 و 180")]
        public double Longitude { get; set; }

        [Display(Name = "الارتفاع عن سطح البحر (متر)")]
        [Range(-500, 9000, ErrorMessage = "الارتفاع يجب أن يكون بين -500 و 9000 متر")]
        public double? ElevationMeters { get; set; }

        [Display(Name = "ارتفاع الهوائي عن الأرض (م)")]
        [Range(0, 500, ErrorMessage = "ارتفاع الهوائي يجب أن يكون بين 0 و 500 متر")]
        public double? AntennaHeightAglMeters { get; set; }

        [Required(ErrorMessage = "الاتجاه مطلوب")]
        [Display(Name = "الاتجاه (درجات)")]
        [Range(0, 360, ErrorMessage = "الاتجاه يجب أن يكون بين 0 و 360 درجة")]
        public double Direction { get; set; }

        [Display(Name = "عدد اللواقط")]
        [NotMapped]
        public int ReceiverCount => Receivers?.Count ?? 0;

        [Display(Name = "عدد المشتركين")]
        [NotMapped]
        public int UserCount => Receivers?.Sum(r => r.UserCount) ?? 0;

        [Required(ErrorMessage = "زاوية الانتشار مطلوبة")]
        [Display(Name = "زاوية الانتشار (درجات)")]
        [Range(0, 360, ErrorMessage = "الزاوية يجب أن تكون بين 0 و 360 درجة")]
        public double CoverageAngle { get; set; }

        [Required(ErrorMessage = "مدى الانتشار مطلوب")]
        [Display(Name = "مدى الانتشار (كم)")]
        [Range(0.1, 1000, ErrorMessage = "المدى يجب أن يكون بين 0.1 و 1000 كم")]
        public double CoverageRange { get; set; }

        [Required(ErrorMessage = "عنوان IP مطلوب")]
        [Display(Name = "عنوان IP")]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
                         ErrorMessage = "عنوان IP غير صحيح")]
        public string? IPAddress { get; set; }

        [Required(ErrorMessage = "قناع الشبكة مطلوب")]
        [Display(Name = "قناع الشبكة")]
        [RegularExpression(@"^((25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$",
                         ErrorMessage = "قناع الشبكة غير صحيح")]
        public string? NetworkMask { get; set; }

        [Display(Name = "واجهة المراقبة الراديوية")]
        [StringLength(100, ErrorMessage = "اسم الواجهة يجب أن لا يتجاوز 100 حرف")]
        public string? RadioInterfaceName { get; set; }

        [Display(Name = "حد تنبيه Noise (dBm)")]
        [Range(-140, -30, ErrorMessage = "يجب أن تكون القيمة بين -140 و -30")]
        public int? NoiseAlertThresholdDbm { get; set; }

        [Display(Name = "حد تنبيه SNR الأدنى (dB)")]
        [Range(0, 80, ErrorMessage = "يجب أن تكون القيمة بين 0 و 80")]
        public int? SnrAlertMinDb { get; set; }

        [Display(Name = "حد تنبيه CCQ الأدنى (%)")]
        [Range(0, 100, ErrorMessage = "يجب أن تكون القيمة بين 0 و 100")]
        public int? CcqAlertMinPercent { get; set; }

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;

        // المفتاح الأجنبي لخادم MikroTik
        [Display(Name = "خادم MikroTik")]
        public int MikroTikServerId { get; set; }

        // علاقة التنقل
        [ForeignKey("MikroTikServerId")]
        [Display(Name = "خادم MikroTik")]
        public virtual MikroTikServer? MikroTikServer { get; set; }

        // علاقة مع Network
        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey("NetworkId")]
        public virtual Network? Network { get; set; }

        // Navigation Properties
        public virtual ICollection<Receiver> Receivers { get; set; } = new List<Receiver>();

        // خاصية جديدة لعرض البروفايلات المتنوعة
        [Display(Name = "البروفايلات")]
        [NotMapped]
        public string? ProfileNames
        {
            get
            {
                if (Receivers == null || Receivers.Count == 0)
                    return null;

                var allProfiles = Receivers
                    .SelectMany(r => r.Clients)
                    .Where(c => !string.IsNullOrEmpty(c.ProfileName))
                    .Select(c => c.ProfileName)
                    .Distinct()
                    .ToList();

                return allProfiles.Count > 0 ? string.Join(", ", allProfiles) : null;
            }
        }
    }
}