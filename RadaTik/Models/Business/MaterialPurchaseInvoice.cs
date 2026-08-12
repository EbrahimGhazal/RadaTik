using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>فاتورة شراء مواد — تُحدّث المستودع ولا تُخصم من المحفظة.</summary>
public class MaterialPurchaseInvoice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public DateTime InvoiceDate { get; set; }

    [MaxLength(120)]
    public string? SupplierName { get; set; }

    /// <summary>مورد ERP مرتبط (اختياري).</summary>
    public int? ErpSupplierId { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Required]
    public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

    public bool IsPaid { get; set; }

    public DateTime? PaidAt { get; set; }

    public bool IsCancelled { get; set; }

    public DateTime? CancelledAt { get; set; }

    /// <summary>آخر عملية محفظة مرتبطة بالدفع (إن وُجد).</summary>
    public int? WalletTransactionId { get; set; }

    public int? MoneyDiaryEntryId { get; set; }

    public int? CashBoxWithdrawalId { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ErpSupplier? ErpSupplier { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ICollection<MaterialPurchaseInvoiceLine> Lines { get; set; } = new List<MaterialPurchaseInvoiceLine>();
}
