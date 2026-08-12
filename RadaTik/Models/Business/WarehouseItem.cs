using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>صنف في مستودع الشركة — للجرد فقط، منفصل عن فواتير النظام الأخرى.</summary>
public class WarehouseItem
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(40)]
    public string? Unit { get; set; }

    [MaxLength(60)]
    public string? Sku { get; set; }

    /// <summary>رقم الموديل — يُستخدم مع الاسم لدمج الأصناف المتشابهة.</summary>
    [MaxLength(60)]
    public string? ModelNumber { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PurchasePrice { get; set; }

    /// <summary>عملة آخر شراء للمادة — تُستخدم عند إدخال سعر مخصص في البيع.</summary>
    public PricingCurrency? PurchaseCurrency { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? WholesalePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? RetailPrice { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ICollection<WarehouseMovement> Movements { get; set; } = new List<WarehouseMovement>();
}
