using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>عميل ERP — سجل تجاري مستقل يمكن ربطه بمشترك ISP.</summary>
public class ErpCustomer
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(30)]
    public string? Phone { get; set; }

    [MaxLength(120)]
    public string? Email { get; set; }

    [MaxLength(250)]
    public string? Address { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>ربط اختياري بمشترك ISP.</summary>
    public int? ClientId { get; set; }

    public bool IsActive { get; set; } = true;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public virtual Network? CompanyNetwork { get; set; }
    public virtual Client? Client { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ICollection<MaterialSalesInvoice> SalesInvoices { get; set; } = new List<MaterialSalesInvoice>();
}
