using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public enum SubscriberReceiverMode
{
    Private = 1,
    Shared = 2
}

public enum SubscriberInstallationInvoiceStatus
{
    Draft = 1,
    PendingWalletPayment = 2,
    PartiallyPaid = 3,
    Paid = 4,
    Cancelled = 5,
    /// <summary>تثبيت نهائي — خُصِم المستودع وبانتظار التحصيل (مسار اللاقط الخاص).</summary>
    Finalized = 6
}

public enum SubscriberInstallationInvoiceKind
{
    InitialSetup = 1,
    ReceiverUpgradeToShared = 2
}

public class SubscriberInstallationInvoice
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int ClientId { get; set; }

    [ForeignKey(nameof(ClientId))]
    public virtual Client? Client { get; set; }

    [Required]
    public int NetworkId { get; set; }

    [ForeignKey(nameof(NetworkId))]
    public virtual Network? Network { get; set; }

    [Required]
    [StringLength(120)]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string ClientName { get; set; } = string.Empty;

    [Required]
    public SubscriberReceiverMode ReceiverMode { get; set; }

    [Required]
    public SubscriberInstallationInvoiceKind Kind { get; set; } = SubscriberInstallationInvoiceKind.InitialSetup;

    [Required]
    public SubscriberInstallationInvoiceStatus Status { get; set; } = SubscriberInstallationInvoiceStatus.PendingWalletPayment;

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal PaidAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal RemainingAmount { get; set; }

    [StringLength(500)]
    public string? ClientSignature { get; set; }

    [StringLength(500)]
    public string? EmployeeSignature { get; set; }

    [Required]
    [StringLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    [ForeignKey(nameof(CreatedByUserId))]
    public virtual ApplicationUser? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public DateTime? FinalizedAt { get; set; }

    [StringLength(450)]
    public string? FinalizedByUserId { get; set; }

    [ForeignKey(nameof(FinalizedByUserId))]
    public virtual ApplicationUser? FinalizedByUser { get; set; }

    public virtual ICollection<SubscriberInstallationInvoiceItem> Items { get; set; } = new List<SubscriberInstallationInvoiceItem>();
}
