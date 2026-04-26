using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace RadTik.Models
{
    public class Receiver
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم اللاقط مطلوب")]
        [Display(Name = "اسم اللاقط")]
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

        [Display(Name = "الارتفاع عن سطح البحر (م)")]
        [Range(-500, 9000, ErrorMessage = "الارتفاع يجب أن يكون بين -500 و 9000 متر")]
        public double? ElevationMeters { get; set; }

        [Display(Name = "ارتفاع الهوائي عن الأرض (م)")]
        [Range(0, 500, ErrorMessage = "ارتفاع الهوائي يجب أن يكون بين 0 و 500 متر")]
        public double? AntennaHeightAglMeters { get; set; }

        [Display(Name = "عدد المشتركين")]
        [NotMapped]
        public int UserCount => Clients?.Count ?? 0;

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

        [Display(Name = "تاريخ الإضافة")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;

        // Foreign Key for Sector
        public int SectorId { get; set; }

        // علاقة مع Network
        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey("NetworkId")]
        public virtual Network? Network { get; set; }

        // Navigation Properties
        [ForeignKey("SectorId")]
        [ValidateNever]
        public virtual Sector Sector { get; set; } = null!;
        public virtual ICollection<Client> Clients { get; set; } = new List<Client>();

        // خاصية محسوبة لعرض اسم خادم MikroTik
        [Display(Name = "خادم MikroTik")]
        [NotMapped]
        public string? MikroTikServerName => Sector?.MikroTikServer?.Name;

        // خاصية جديدة لعرض البروفايلات المتنوعة للمشتركين
        [Display(Name = "البروفايلات")]
        [NotMapped]
        public string? ProfileNames => Clients != null ?
            string.Join(", ", Clients.Select(c => c.ProfileName).Distinct()) : null;
    }
}