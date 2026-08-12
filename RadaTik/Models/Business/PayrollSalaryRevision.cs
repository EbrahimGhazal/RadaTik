using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>سجل تاريخي لزيادة راتب الموظف.</summary>
public class PayrollSalaryRevision
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public int PayrollEmployeeId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal PreviousSalary { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal NewSalary { get; set; }

    [Required]
    public PayrollSalaryAdjustmentType AdjustmentType { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal AdjustmentValue { get; set; }

    public DateTime EffectiveDate { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual PayrollEmployee? PayrollEmployee { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
}
