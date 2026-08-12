using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>
/// قيد في دفتر الإيراد والمصروف — تتبع بسيط لما دخل وخرج نقداً أو بنكياً خارج المحفظة الإلكترونية.
/// </summary>
public class MoneyDiaryEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public MoneyDiaryEntryType EntryType { get; set; }

    [Required]
    [MaxLength(64)]
    public string CategoryKey { get; set; } = string.Empty;

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    /// <summary>عملة القيد — منفصل عن محفظة RadaTik.</summary>
    [Required]
    public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

    [Required]
    public DateTime EntryDate { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int? MaterialPurchaseInvoiceId { get; set; }

    public int? MaterialSalesInvoiceId { get; set; }

    public int? PayrollPaymentId { get; set; }

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
}
