using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

public class MaterialSalesInvoiceLine
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int MaterialSalesInvoiceId { get; set; }

    [Required]
    public int WarehouseItemId { get; set; }

    [Required]
    public MaterialSalePriceMode PriceMode { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }

    public virtual MaterialSalesInvoice? Invoice { get; set; }
    public virtual WarehouseItem? WarehouseItem { get; set; }
}
