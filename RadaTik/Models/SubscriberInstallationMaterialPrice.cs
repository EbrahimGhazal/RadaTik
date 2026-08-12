using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public class SubscriberInstallationMaterialPrice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    [Required]
    [StringLength(60)]
    public string MaterialKey { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string MaterialName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    /// <summary>صنف المستودع المرتبط (للخصم عند التثبيت).</summary>
    public int? WarehouseItemId { get; set; }

    [ForeignKey(nameof(WarehouseItemId))]
    public virtual Business.WarehouseItem? WarehouseItem { get; set; }

    public virtual ICollection<SubscriberInstallationMaterialWarehouseLink> WarehouseLinks { get; set; } =
        new List<SubscriberInstallationMaterialWarehouseLink>();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
