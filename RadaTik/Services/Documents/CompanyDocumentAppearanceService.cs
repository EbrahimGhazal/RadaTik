using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Domain.Common;
using RadaTik.Helpers;
using RadaTik.Models;

namespace RadaTik.Services.Documents;

public sealed class CompanyDocumentAppearanceService(
    ApplicationDbContext context,
    IWebHostEnvironment environment)
    : ApplicationServiceBase(context), ICompanyDocumentAppearanceService
{
    private const string DefaultColor = "#1B3A4B";
    private static readonly Regex HexColorRegex = new(@"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public async Task<int?> ResolveCompanyNetworkIdAsync(int selectedNetworkId, CancellationToken ct = default)
    {
        Network? selected = await Db.Networks.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
        if (selected == null)
        {
            return null;
        }

        return selected.ParentNetworkId ?? selected.Id;
    }

    public async Task<CompanyDocumentChrome> GetChromeAsync(
        int selectedOrCompanyNetworkId,
        string? documentTitle = null,
        string? subtitle = null,
        string? generatedAt = null,
        CancellationToken ct = default)
    {
        int? companyId = await ResolveCompanyNetworkIdAsync(selectedOrCompanyNetworkId, ct);
        if (!companyId.HasValue)
        {
            return BuildDefaultChrome(0, "الشركة", documentTitle, subtitle, generatedAt, logoUrl: null);
        }

        Network company = await Db.Networks.AsNoTracking()
            .FirstAsync(n => n.Id == companyId.Value, ct);

        CompanyDocumentAppearance? row = await Db.CompanyDocumentAppearances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyNetworkId == companyId.Value, ct);

        return BuildChrome(company, row, documentTitle, subtitle, generatedAt);
    }

    public async Task<CompanyDocumentAppearanceEditor> GetEditorAsync(int companyNetworkId, CancellationToken ct = default)
    {
        Network company = await Db.Networks.AsNoTracking()
            .FirstAsync(n => n.Id == companyNetworkId, ct);
        CompanyDocumentAppearance? row = await Db.CompanyDocumentAppearances.AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyNetworkId == companyNetworkId, ct);

        return new CompanyDocumentAppearanceEditor
        {
            CompanyName = company.Name ?? "الشركة",
            HeaderLayout = row?.HeaderLayout ?? DocumentHeaderLayout.ClassicSplit,
            ShowLogo = row?.ShowLogo ?? true,
            UseNetworkLogo = row?.UseNetworkLogo ?? true,
            CustomLogoPath = row?.CustomLogoPath,
            NetworkLogoPath = company.LogoPath,
            PrimaryColor = NormalizeColor(row?.PrimaryColor),
            TableHeaderColor = NormalizeColor(row?.TableHeaderColor),
            WatermarkMode = row?.WatermarkMode ?? DocumentWatermarkMode.None,
            WatermarkText = row?.WatermarkText,
            WatermarkOpacityPercent = ClampOpacity(row?.WatermarkOpacityPercent ?? 12),
            TableDensity = row?.TableDensity ?? DocumentTableDensity.Comfortable,
            StripedRows = row?.StripedRows ?? true,
            FooterText = row?.FooterText,
            ShowGeneratedAt = row?.ShowGeneratedAt ?? true
        };
    }

    public async Task SaveAsync(
        int companyNetworkId,
        string userId,
        CompanyDocumentAppearanceSaveCommand command,
        CancellationToken ct = default)
    {
        bool companyExists = await Db.Networks.AnyAsync(
            n => n.Id == companyNetworkId && n.ParentNetworkId == null,
            ct);
        if (!companyExists)
        {
            throw new InvalidOperationException("هوية المستندات تُحفظ للشركة الرئيسية فقط.");
        }

        CompanyDocumentAppearance? row = await Db.CompanyDocumentAppearances
            .FirstOrDefaultAsync(a => a.CompanyNetworkId == companyNetworkId, ct);

        if (row == null)
        {
            row = new CompanyDocumentAppearance { CompanyNetworkId = companyNetworkId };
            Db.CompanyDocumentAppearances.Add(row);
        }

        row.HeaderLayout = command.HeaderLayout;
        row.ShowLogo = command.ShowLogo;
        row.UseNetworkLogo = command.UseNetworkLogo;
        row.PrimaryColor = NormalizeColor(command.PrimaryColor);
        row.TableHeaderColor = NormalizeColor(command.TableHeaderColor);
        row.WatermarkMode = command.WatermarkMode;
        row.WatermarkText = string.IsNullOrWhiteSpace(command.WatermarkText)
            ? null
            : command.WatermarkText.Trim();
        if (row.WatermarkText != null && row.WatermarkText.Length > 80)
        {
            row.WatermarkText = row.WatermarkText[..80];
        }

        row.WatermarkOpacityPercent = ClampOpacity(command.WatermarkOpacityPercent);
        row.TableDensity = command.TableDensity;
        row.StripedRows = command.StripedRows;
        row.FooterText = string.IsNullOrWhiteSpace(command.FooterText) ? null : command.FooterText.Trim();
        if (row.FooterText != null && row.FooterText.Length > 250)
        {
            row.FooterText = row.FooterText[..250];
        }

        row.ShowGeneratedAt = command.ShowGeneratedAt;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedByUserId = userId;

        if (command.RemoveCustomLogo && !string.IsNullOrWhiteSpace(row.CustomLogoPath))
        {
            DeleteLogoFile(row.CustomLogoPath);
            row.CustomLogoPath = null;
            row.UseNetworkLogo = true;
        }

        if (command.LogoFile != null && command.LogoFile.Length > 0 && !ImageUploadRules.IsTooLarge(command.LogoFile))
        {
            string? saved = await SaveLogoFileAsync(command.LogoFile, ct);
            if (!string.IsNullOrWhiteSpace(saved))
            {
                if (!string.IsNullOrWhiteSpace(row.CustomLogoPath))
                {
                    DeleteLogoFile(row.CustomLogoPath);
                }

                row.CustomLogoPath = saved;
                row.UseNetworkLogo = false;
                row.ShowLogo = true;
            }
        }

        await Db.SaveChangesAsync(ct);
    }

    private static CompanyDocumentChrome BuildChrome(
        Network company,
        CompanyDocumentAppearance? row,
        string? documentTitle,
        string? subtitle,
        string? generatedAt)
    {
        bool showLogo = row?.ShowLogo ?? true;
        bool useNetworkLogo = row?.UseNetworkLogo ?? true;
        string? logoUrl = null;
        if (showLogo)
        {
            logoUrl = !useNetworkLogo && !string.IsNullOrWhiteSpace(row?.CustomLogoPath)
                ? row.CustomLogoPath
                : company.LogoPath;
        }

        string companyName = company.Name ?? "الشركة";
        DocumentWatermarkMode watermarkMode = row?.WatermarkMode ?? DocumentWatermarkMode.None;
        string? watermarkText = watermarkMode switch
        {
            DocumentWatermarkMode.CompanyName => companyName,
            DocumentWatermarkMode.CustomText => string.IsNullOrWhiteSpace(row?.WatermarkText)
                ? companyName
                : row.WatermarkText,
            _ => null
        };

        return new CompanyDocumentChrome
        {
            CompanyNetworkId = company.Id,
            CompanyName = companyName,
            DocumentTitle = documentTitle,
            Subtitle = subtitle,
            LogoUrl = logoUrl,
            HeaderLayout = row?.HeaderLayout ?? DocumentHeaderLayout.ClassicSplit,
            PrimaryColor = NormalizeColor(row?.PrimaryColor),
            TableHeaderColor = NormalizeColor(row?.TableHeaderColor),
            WatermarkMode = watermarkMode,
            WatermarkText = watermarkText,
            WatermarkOpacityPercent = ClampOpacity(row?.WatermarkOpacityPercent ?? 12),
            TableDensity = row?.TableDensity ?? DocumentTableDensity.Comfortable,
            StripedRows = row?.StripedRows ?? true,
            FooterText = row?.FooterText,
            ShowGeneratedAt = row?.ShowGeneratedAt ?? true,
            GeneratedAt = generatedAt
        };
    }

    private static CompanyDocumentChrome BuildDefaultChrome(
        int companyNetworkId,
        string companyName,
        string? documentTitle,
        string? subtitle,
        string? generatedAt,
        string? logoUrl) =>
        new()
        {
            CompanyNetworkId = companyNetworkId,
            CompanyName = companyName,
            DocumentTitle = documentTitle,
            Subtitle = subtitle,
            LogoUrl = logoUrl,
            GeneratedAt = generatedAt
        };

    private static string NormalizeColor(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw) && HexColorRegex.IsMatch(raw.Trim()))
        {
            return raw.Trim().ToUpperInvariant();
        }

        return DefaultColor;
    }

    private static int ClampOpacity(int value) => Math.Clamp(value, 5, 40);

    private async Task<string?> SaveLogoFileAsync(IFormFile file, CancellationToken ct)
    {
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (ext is not (".png" or ".jpg" or ".jpeg" or ".webp" or ".gif"))
        {
            return null;
        }

        string uploadsFolder = Path.Combine(environment.WebRootPath, "uploads", "document-brand");
        Directory.CreateDirectory(uploadsFolder);
        string uniqueFileName = $"{Guid.NewGuid():N}{ext}";
        string filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using FileStream stream = new(filePath, FileMode.Create);
        await file.CopyToAsync(stream, ct);
        return $"/uploads/document-brand/{uniqueFileName}";
    }

    private void DeleteLogoFile(string publicPath)
    {
        if (string.IsNullOrWhiteSpace(publicPath) || !publicPath.StartsWith("/uploads/document-brand/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string fullPath = Path.Combine(environment.WebRootPath, publicPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
    }
}
