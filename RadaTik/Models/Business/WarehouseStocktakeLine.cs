using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

public class WarehouseStocktakeLine
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int WarehouseStocktakeId { get; set; }

    [Required]
    public int WarehouseItemId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal SystemQuantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal CountedQuantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal Difference { get; set; }

    public virtual WarehouseStocktake? Stocktake { get; set; }
    public virtual WarehouseItem? WarehouseItem { get; set; }
}
