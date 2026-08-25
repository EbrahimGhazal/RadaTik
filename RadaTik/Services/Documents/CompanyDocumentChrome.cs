using RadaTik.Models;

namespace RadaTik.Services.Documents;

public sealed class CompanyDocumentChrome
{
    public required int CompanyNetworkId { get; init; }
    public required string CompanyName { get; init; }
    public string? DocumentTitle { get; init; }
    public string? Subtitle { get; init; }
    public string? LogoUrl { get; init; }
    public DocumentHeaderLayout HeaderLayout { get; init; }
    public string PrimaryColor { get; init; } = "#1B3A4B";
    public string TableHeaderColor { get; init; } = "#1B3A4B";
    public DocumentWatermarkMode WatermarkMode { get; init; }
    public string? WatermarkText { get; init; }
    public int WatermarkOpacityPercent { get; init; } = 12;
    public DocumentTableDensity TableDensity { get; init; }
    public bool StripedRows { get; init; } = true;
    public string? FooterText { get; init; }
    public bool ShowGeneratedAt { get; init; } = true;
    public string? GeneratedAt { get; init; }
}

public sealed class CompanyDocumentAppearanceEditor
{
    public DocumentHeaderLayout HeaderLayout { get; init; } = DocumentHeaderLayout.ClassicSplit;
    public bool ShowLogo { get; init; } = true;
    public bool UseNetworkLogo { get; init; } = true;
    public string? CustomLogoPath { get; init; }
    public string? NetworkLogoPath { get; init; }
    public string PrimaryColor { get; init; } = "#1B3A4B";
    public string TableHeaderColor { get; init; } = "#1B3A4B";
    public DocumentWatermarkMode WatermarkMode { get; init; } = DocumentWatermarkMode.None;
    public string? WatermarkText { get; init; }
    public int WatermarkOpacityPercent { get; init; } = 12;
    public DocumentTableDensity TableDensity { get; init; } = DocumentTableDensity.Comfortable;
    public bool StripedRows { get; init; } = true;
    public string? FooterText { get; init; }
    public bool ShowGeneratedAt { get; init; } = true;
    public required string CompanyName { get; init; }
}

public sealed class CompanyDocumentAppearanceSaveCommand
{
    public DocumentHeaderLayout HeaderLayout { get; init; }
    public bool ShowLogo { get; init; } = true;
    public bool UseNetworkLogo { get; init; } = true;
    public bool RemoveCustomLogo { get; init; }
    public string? PrimaryColor { get; init; }
    public string? TableHeaderColor { get; init; }
    public DocumentWatermarkMode WatermarkMode { get; init; }
    public string? WatermarkText { get; init; }
    public int WatermarkOpacityPercent { get; init; } = 12;
    public DocumentTableDensity TableDensity { get; init; }
    public bool StripedRows { get; init; } = true;
    public string? FooterText { get; init; }
    public bool ShowGeneratedAt { get; init; } = true;
    public IFormFile? LogoFile { get; init; }
}
