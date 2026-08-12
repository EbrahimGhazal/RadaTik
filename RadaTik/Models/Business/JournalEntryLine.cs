using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models.Business;

public class JournalEntryLine
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int JournalEntryId { get; set; }

    [Required]
    public int ChartOfAccountId { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Debit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Credit { get; set; }

    [MaxLength(250)]
    public string? LineDescription { get; set; }

    public virtual JournalEntry? JournalEntry { get; set; }
    public virtual ChartOfAccount? ChartOfAccount { get; set; }
}
