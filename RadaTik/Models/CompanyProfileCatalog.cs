using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

/// <summary>
/// تعريف بروفايل (سرعة) موحّد على مستوى الشركة — يُنشر لاحقاً على سيرفرات MikroTik متعددة.
/// </summary>
public class CompanyProfileCatalog
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    [Display(Name = "شركة")]
    public int CompanyNetworkId { get; set; }

    [ForeignKey(nameof(CompanyNetworkId))]
    public virtual Network? CompanyNetwork { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "اسم البروفايل")]
    public string Name { get; set; } = null!;

    [Display(Name = "الوصف")]
    public string? Description { get; set; }

    [Required]
    public ProfileType Type { get; set; }

    [Required]
    public BillingCycle BillingCycle { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    public decimal VATPercentage { get; set; } = 15;

    public int DownloadSpeed { get; set; }
    public SpeedUnit DownloadSpeedUnit { get; set; } = SpeedUnit.Mbps;
    public int? UploadSpeed { get; set; }
    public SpeedUnit? UploadSpeedUnit { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal? DataLimit { get; set; }
    public int? TimeLimit { get; set; }
    public int? IPTVDevices { get; set; }
    public bool IsDataCapped { get; set; }
    public bool IsTimeCapped { get; set; }
    public int MaxUsers { get; set; } = 1;
    public int MinDevices { get; set; } = 1;
    public int MaxDevices { get; set; } = 1;
    public string? AllowedPorts { get; set; }
    public string? AllowedAddresses { get; set; }
    public string? Features { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsForNewClients { get; set; } = true;
    public int DisplayOrder { get; set; }

    public string? MikroTikLocalAddress { get; set; }
    public string? MikroTikRemoteAddress { get; set; }
    public string? MikroTikRateLimit { get; set; }
    public bool MikroTikOnlyOne { get; set; } = true;
    public string? MikroTikService { get; set; } = "pppoe";

    public DateTime CreatedDate { get; set; } = DateTime.Now;
    public DateTime UpdatedDate { get; set; } = DateTime.Now;

    public virtual ICollection<Profile> Deployments { get; set; } = new List<Profile>();
}
