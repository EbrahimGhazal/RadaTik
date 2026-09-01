using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>رابط صفحة سوشال ميديا تابع للشركة ويظهر للمشترك عند تفعيله.</summary>
public class CompanySocialLink
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [ForeignKey(nameof(CompanyNetworkId))]
    public virtual Network? CompanyNetwork { get; set; }

    public SocialMediaPlatform Platform { get; set; }

    [Required]
    [StringLength(80)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Url { get; set; } = string.Empty;

    public bool IsVisibleToClients { get; set; } = true;

    public int SortOrder { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
