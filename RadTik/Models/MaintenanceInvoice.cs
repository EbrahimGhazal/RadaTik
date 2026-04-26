using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadTik.Models;

public enum MaintenanceInvoiceStatus
{
    Pending = 1,
    Paid = 2,
    Cancelled = 3
}

public enum MaintenanceCommissionMode
{
    Percent = 1,
    Fixed = 2
}

/// <summary>
/// One invoice per completed maintenance request.
/// Stores pricing snapshots so historical invoices stay immutable.
/// </summary>
public class MaintenanceInvoice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int MaintenanceRequestId { get; set; }

    [ForeignKey(nameof(MaintenanceRequestId))]
    public virtual MaintenanceRequest? MaintenanceRequest { get; set; }

    [Required]
    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    [Required]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    [Required]
    [StringLength(450)]
    public string IssuedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(IssuedByUserId))]
    public virtual ApplicationUser? IssuedByUser { get; set; }

    [Required]
    [StringLength(1000)]
    public string FaultExplanation { get; set; } = string.Empty;

    [Required]
    [StringLength(1000)]
    public string FixExplanation { get; set; } = string.Empty;

    [Column(TypeName = "decimal(18,2)")]
    public decimal ServiceBasePrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TransportFee { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal GrossAmount { get; set; }

    [Required]
    public MaintenanceCommissionMode CommissionMode { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommissionValue { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal CommissionAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal NetAmountToCompany { get; set; }

    [Required]
    public MaintenanceInvoiceStatus Status { get; set; } = MaintenanceInvoiceStatus.Pending;

    public int? PaymentTransactionId { get; set; }

    [ForeignKey(nameof(PaymentTransactionId))]
    public virtual PaymentTransaction? PaymentTransaction { get; set; }

    [StringLength(450)]
    public string? PaidByUserId { get; set; }

    [ForeignKey(nameof(PaidByUserId))]
    public virtual ApplicationUser? PaidByUser { get; set; }

    public DateTime? PaidAt { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? PreviousClientBalance { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? NewClientBalance { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
