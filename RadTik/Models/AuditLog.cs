using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// سجل تدقيق للعمليات (Audit Trail) لتسهيل الإدارة والبحث والأرشفة لاحقاً.
    /// </summary>
    public class AuditLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Display(Name = "التاريخ")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(450)]
        public string? UserId { get; set; }

        [StringLength(256)]
        public string? UserName { get; set; }

        [StringLength(500)]
        public string? Roles { get; set; }

        [StringLength(50)]
        public string? HttpMethod { get; set; }

        [StringLength(200)]
        public string? Controller { get; set; }

        [StringLength(200)]
        public string? Action { get; set; }

        [StringLength(500)]
        public string? Path { get; set; }

        public int? StatusCode { get; set; }

        public int? NetworkId { get; set; }

        [StringLength(200)]
        public string? EntityType { get; set; }

        [StringLength(100)]
        public string? EntityId { get; set; }

        [StringLength(1000)]
        public string? Summary { get; set; }
    }
}

