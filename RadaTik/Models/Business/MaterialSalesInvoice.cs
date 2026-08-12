using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>فاتورة بيع مواد — مستقلة عن فواتير تجهيز المشتركين.</summary>
public class MaterialSalesInvoice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public DateTime InvoiceDate { get; set; }

    [MaxLength(120)]
    public string? CustomerName { get; set; }

    /// <summary>عميل ERP مرتبط (اختياري).</summary>
    public int? ErpCustomerId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

    public bool IsPaid { get; set; }

    public DateTime? PaidAt { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancelledAt { get; set; }

    public int? WalletTransactionId { get; set; }

    public int? MoneyDiaryEntryId { get; set; }

    public int? CashBoxDepositId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ErpCustomer? ErpCustomer { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ICollection<MaterialSalesInvoiceLine> Lines { get; set; } = new List<MaterialSalesInvoiceLine>();
}
