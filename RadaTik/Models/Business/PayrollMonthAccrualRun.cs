using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>سجل تشغيل تسوية نهاية الشهر لرواتب شركة (مرة واحدة لكل شهر).</summary>
public class PayrollMonthAccrualRun
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    [Range(2000, 2100)]
    public int Year { get; set; }

    [Required]
    [Range(1, 12)]
    public int Month { get; set; }

    public DateTime RunAt { get; set; } = DateTime.UtcNow;

    public int EmployeesProcessed { get; set; }

    public virtual Network? CompanyNetwork { get; set; }
}
