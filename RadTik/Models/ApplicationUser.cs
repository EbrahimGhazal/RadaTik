using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    public class ApplicationUser : IdentityUser
    {
        [Display(Name = "الاسم الكامل")]
        [StringLength(100)]
        public string? FullName { get; set; }

        [Display(Name = "رقم الهاتف")]
        [StringLength(20)]
        public override string? PhoneNumber { get; set; }

        [Display(Name = "العنوان")]
        [StringLength(500)]
        public string? Address { get; set; }

        [Display(Name = "QR شام كاش")]
        [StringLength(500)]
        public string? ShamCashQrCodePath { get; set; }

        [Display(Name = "تاريخ التسجيل")]
        public DateTime CreatedDate { get; set; } = DateTime.Now;

        [Display(Name = "آخر تحديث")]
        public DateTime? LastUpdated { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        // علاقة مع Client - للربط بين المستخدم والعميل
        [Display(Name = "معرف العميل")]
        public int? ClientId { get; set; }

        [ForeignKey("ClientId")]
        public virtual Client? Client { get; set; }

        // علاقة مع Network - للربط بين المستخدم والشبكة
        [Display(Name = "معرف الشبكة")]
        public int? NetworkId { get; set; }

        [ForeignKey("NetworkId")]
        public virtual Network? Network { get; set; }
    }
}
