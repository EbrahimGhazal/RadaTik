using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>تحويل نقد بين عملتي صندوق الشركة (ل.س.ج ↔ $) مع سعر صرف مسجّل.</summary>
public class CashBoxCurrencyExchange
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CashBoxId { get; set; }

    [ForeignKey(nameof(CashBoxId))]
    public virtual CashBox? CashBox { get; set; }

    [Required]
    public PricingCurrency FromCurrency { get; set; }

    [Required]
    public PricingCurrency ToCurrency { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal SourceAmount { get; set; }

    /// <summary>1 USD = ExchangeRate ل.س.ج جديدة.</summary>
    [Required]
    [Column(TypeName = "decimal(18,4)")]
    public decimal ExchangeRate { get; set; }

    [Required]
    [Column(TypeName = "decimal(18,2)")]
    public decimal TargetAmount { get; set; }

    [Required]
    public int CashBoxWithdrawalId { get; set; }

    [ForeignKey(nameof(CashBoxWithdrawalId))]
    public virtual CashBoxWithdrawal? Withdrawal { get; set; }

    [Required]
    public int CashBoxDepositId { get; set; }

    [ForeignKey(nameof(CashBoxDepositId))]
    public virtual CashBoxDeposit? Deposit { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
