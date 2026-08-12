using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>قيد محاسبي — مدين ودائن.</summary>
public class JournalEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    public DateTime EntryDate { get; set; }

    [Required]
    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? ReferenceNumber { get; set; }

    public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

    [Required]
    public PricingCurrency Currency { get; set; } = PricingCurrency.SYP_New;

    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    [MaxLength(450)]
    public string? PostedByUserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PostedAt { get; set; }

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ApplicationUser? CreatedByUser { get; set; }
    public virtual ApplicationUser? PostedByUser { get; set; }
    public virtual ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
}
