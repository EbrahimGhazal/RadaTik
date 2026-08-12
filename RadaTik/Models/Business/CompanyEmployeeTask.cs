using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>مهمة موظف — تعيين وإنجاز ومتابعة.</summary>
public class CompanyEmployeeTask
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; set; }

    [Required]
    [MaxLength(450)]
    public string AssignedToUserId { get; set; } = string.Empty;

    [MaxLength(450)]
    public string? AssignedByUserId { get; set; }

    public CompanyEmployeeTaskStatus Status { get; set; } = CompanyEmployeeTaskStatus.Pending;

    public CompanyEmployeeTaskPriority Priority { get; set; } = CompanyEmployeeTaskPriority.Normal;

    public DateTime? DueDate { get; set; }

    public DateTime? CompletedAt { get; set; }

    [MaxLength(1000)]
    public string? CompletionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ApplicationUser? AssignedToUser { get; set; }
    public virtual ApplicationUser? AssignedByUser { get; set; }
}
