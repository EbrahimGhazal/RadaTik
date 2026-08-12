using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models.Business;

namespace RadaTik.Models;

/// <summary>
/// ربط مادة تسعير التركيب بعدة أصناف/موديلات من المستودع.
/// </summary>
public class SubscriberInstallationMaterialWarehouseLink
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int MaterialPriceId { get; set; }

    [ForeignKey(nameof(MaterialPriceId))]
    public virtual SubscriberInstallationMaterialPrice? MaterialPrice { get; set; }

    [Required]
    public int WarehouseItemId { get; set; }

    [ForeignKey(nameof(WarehouseItemId))]
    public virtual WarehouseItem? WarehouseItem { get; set; }

    /// <summary>الموديل الافتراضي عند إنشاء فاتورة التركيب.</summary>
    public bool IsDefault { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
