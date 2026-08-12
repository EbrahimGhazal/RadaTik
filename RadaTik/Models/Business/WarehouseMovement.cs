using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

/// <summary>حركة مخزون (وارد / صادر / تصحيح) — تُحدَّث الكمية من مجموع الحركات فقط.</summary>
public class WarehouseMovement
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public int WarehouseItemId { get; set; }

    [Required]
    public WarehouseMovementType MovementType { get; set; }

    /// <summary>للوارد والصادر: كمية موجبة. للتصحيح: موجبة أو سالبة.</summary>
    [Required]
    [Column(TypeName = "decimal(18,3)")]
    public decimal Quantity { get; set; }

    [Required]
    public DateTime MovementDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public int? MaterialPurchaseInvoiceId { get; set; }

    public int? MaterialSalesInvoiceId { get; set; }

    public int? WarehouseStocktakeId { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual WarehouseItem? WarehouseItem { get; set; }
    public virtual MaterialPurchaseInvoice? MaterialPurchaseInvoice { get; set; }
    public virtual MaterialSalesInvoice? MaterialSalesInvoice { get; set; }
    public virtual WarehouseStocktake? WarehouseStocktake { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
}
