using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public class SubscriberInstallationInvoicePayment
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int SubscriberInstallationInvoiceId { get; set; }

    [ForeignKey(nameof(SubscriberInstallationInvoiceId))]
    public virtual SubscriberInstallationInvoice? SubscriberInstallationInvoice { get; set; }

    public int? PaymentTransactionId { get; set; }

    [ForeignKey(nameof(PaymentTransactionId))]
    public virtual PaymentTransaction? PaymentTransaction { get; set; }

    public SubscriberInstallationPaymentMethod PaymentMethod { get; set; } = SubscriberInstallationPaymentMethod.Wallet;

    [Column(TypeName = "decimal(18,2)")]
    public decimal Amount { get; set; }

    [Required]
    [StringLength(450)]
    public string ReceivedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(ReceivedByUserId))]
    public virtual ApplicationUser? ReceivedByUser { get; set; }

    [StringLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
