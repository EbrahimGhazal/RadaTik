using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public class SubscriberInstallationInvoiceItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int SubscriberInstallationInvoiceId { get; set; }

    [ForeignKey(nameof(SubscriberInstallationInvoiceId))]
    public virtual SubscriberInstallationInvoice? SubscriberInstallationInvoice { get; set; }

    [Required]
    [StringLength(120)]
    public string ItemName { get; set; } = string.Empty;

    [StringLength(60)]
    public string? MaterialKey { get; set; }

    /// <summary>إن كان true يُخصم من المستودع عند التثبيت النهائي.</summary>
    public bool IsStockItem { get; set; }

    public int? WarehouseItemId { get; set; }

    [ForeignKey(nameof(WarehouseItemId))]
    public virtual Business.WarehouseItem? WarehouseItem { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Quantity { get; set; } = 1m;

    [Column(TypeName = "decimal(18,2)")]
    public decimal LineTotal { get; set; }
}
