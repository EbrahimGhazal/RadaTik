using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models
{
    /// <summary>
    /// ربط مستخدم بصلاحية (Granted)
    /// </summary>
    public class UserPermission
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey(nameof(UserId))]
        public virtual ApplicationUser? User { get; set; }

        [Required]
        public int PermissionId { get; set; }

        [ForeignKey(nameof(PermissionId))]
        public virtual Permission? Permission { get; set; }

        [Display(Name = "تاريخ الإسناد")]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}

