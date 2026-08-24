using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RadaTik.Models;

public enum DocumentHeaderLayout
{
    ClassicSplit = 0,
    CenteredLogo = 1,
    ColorBar = 2,
    Minimal = 3
}

public enum DocumentWatermarkMode
{
    None = 0,
    CompanyName = 1,
    Logo = 2,
    CustomText = 3
}

public enum DocumentTableDensity
{
    Comfortable = 0,
    Compact = 1,
    Wide = 2
}

/// <summary>
/// هوية مستندات الشركة (تقارير وعقود). صف واحد لكل شركة رئيسية، لا يُشارك مع شركات أخرى.
/// </summary>
public class CompanyDocumentAppearance
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    [Required]
    public int CompanyNetworkId { get; set; }

    [ForeignKey(nameof(CompanyNetworkId))]
    public virtual Network? CompanyNetwork { get; set; }

    public DocumentHeaderLayout HeaderLayout { get; set; } = DocumentHeaderLayout.ClassicSplit;

    public bool ShowLogo { get; set; } = true;

    public bool UseNetworkLogo { get; set; } = true;

    [StringLength(500)]
    public string? CustomLogoPath { get; set; }

    [StringLength(7)]
    public string PrimaryColor { get; set; } = "#1B3A4B";

    [StringLength(7)]
    public string TableHeaderColor { get; set; } = "#1B3A4B";

    public DocumentWatermarkMode WatermarkMode { get; set; } = DocumentWatermarkMode.None;

    [StringLength(80)]
    public string? WatermarkText { get; set; }

    /// <summary>شفافية العلامة المائية من 5 إلى 40.</summary>
    public int WatermarkOpacityPercent { get; set; } = 12;

    public DocumentTableDensity TableDensity { get; set; } = DocumentTableDensity.Comfortable;

    public bool StripedRows { get; set; } = true;

    [StringLength(250)]
    public string? FooterText { get; set; }

    public bool ShowGeneratedAt { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [StringLength(450)]
    public string? UpdatedByUserId { get; set; }
}
