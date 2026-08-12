using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

/// <summary>جلسة جرد — تُنشئ حركات تصحيح عند الاعتماد.</summary>
public class WarehouseStocktake
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public DateTime StocktakeDate { get; set; }

    public DateTime? PeriodFrom { get; set; }

    public DateTime? PeriodTo { get; set; }

    public int? WarehouseItemId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual WarehouseItem? WarehouseItem { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ICollection<WarehouseStocktakeLine> Lines { get; set; } = new List<WarehouseStocktakeLine>();
}
