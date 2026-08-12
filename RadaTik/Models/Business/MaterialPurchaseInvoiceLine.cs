using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

public class MaterialPurchaseInvoiceLine
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int MaterialPurchaseInvoiceId { get; set; }

    public int? WarehouseItemId { get; set; }

    [Required]
    [MaxLength(120)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(60)]
    public string? ModelNumber { get; set; }

    [Required]
    public MaterialPackageUnit PackageUnit { get; set; } = MaterialPackageUnit.Piece;

    /// <summary>عدد القطع داخل العلبة/الكرتونة/الربطة (1 للقطعة).</summary>
    [Required]
    public int UnitsPerPackage { get; set; } = 1;

    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal PackageQuantity { get; set; }

    /// <summary>الكمية المحوّلة إلى قطع في المستودع.</summary>
    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal BaseQuantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? WholesalePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RetailPrice { get; set; }

    public virtual MaterialPurchaseInvoice? Invoice { get; set; }
    public virtual WarehouseItem? WarehouseItem { get; set; }
}
