using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using RadaTik.Models;

namespace RadaTik.Models.Business;

/// <summary>حساب في شجرة الحسابات المحاسبية.</summary>
public class ChartOfAccount
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [Required]
    [MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(120)]
    public string Name { get; set; } = string.Empty;

    public ChartOfAccountType AccountType { get; set; }

    public int? ParentAccountId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Network? CompanyNetwork { get; set; }
    public virtual ChartOfAccount? ParentAccount { get; set; }
    public virtual ICollection<ChartOfAccount> ChildAccounts { get; set; } = new List<ChartOfAccount>();
    public virtual ICollection<JournalEntryLine> JournalLines { get; set; } = new List<JournalEntryLine>();
}
