using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// ميزات/خدمات الشركة (Network) المتاحة أو المفعّلة (Entitlements / Feature Flags).
    /// تُستخدم لاحقاً لربط الخدمات المدفوعة بخطة الشركة أو مشترياتها.
    /// </summary>
    public class NetworkFeature
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int NetworkId { get; set; }

        [ForeignKey(nameof(NetworkId))]
        public virtual Network? Network { get; set; }

        /// <summary>
        /// مفتاح الميزة (مثل: "MikroTikServers", "Profiles", "Clients"...)
        /// </summary>
        [Required]
        [StringLength(100)]
        public string Key { get; set; } = null!;

        /// <summary>
        /// هل الميزة مفعّلة لهذه الشركة؟
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}

