using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>رقم شكاوى تابع للشركة ويظهر للمشترك عند تفعيله.</summary>
public class CompanyComplaintContact
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [ForeignKey(nameof(CompanyNetworkId))]
    public virtual Network? CompanyNetwork { get; set; }

    [Required]
    [StringLength(80)]
    public string Label { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string PhoneNumber { get; set; } = string.Empty;

    public bool IsVisibleToClients { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
