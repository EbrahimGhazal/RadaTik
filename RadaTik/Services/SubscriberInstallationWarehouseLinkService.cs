using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.Models.Business;

namespace RadaTik.Services;

/// <summary>
/// ربط مواد تسعير التركيب بعدة موديلات/أصناف من المستودع.
/// </summary>
public sealed class SubscriberInstallationWarehouseLinkService
{
    private static readonly string[] ServiceMaterialKeys = ["labor", "transport", "account_setup"];

    private static readonly IReadOnlyDictionary<string, string[]> MaterialSearchTerms = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        ["receiver"] = ["مستقبل", "لاقط", "receiver", "antenna"],
        ["cable"] = ["كبل", "كابل", "cable"],
        ["rg"] = ["rg", "كابلات rg", "كابل rg"],
        ["switch"] = ["سويتش", "switch", "مفتاح"],
        ["router"] = ["راوتر", "router", "راوترات"]
    };

    private readonly ApplicationDbContext _context;
    private readonly IWarehouseStockService _warehouseStock;

    public SubscriberInstallationWarehouseLinkService(
        ApplicationDbContext context,
        IWarehouseStockService warehouseStock)
    {
        _context = context;
        _warehouseStock = warehouseStock;
    }

    public static bool IsStockMaterialKey(string materialKey) =>
        !ServiceMaterialKeys.Contains(materialKey, StringComparer.OrdinalIgnoreCase);

    public async Task<AutoLinkWarehouseResult> AutoLinkAsync(int networkId, CancellationToken cancellationToken = default)
    {
        int companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId, cancellationToken);
        List<WarehouseItem> warehouseItems = await _context.WarehouseItems
            .AsNoTracking()
            .Where(w => w.CompanyNetworkId == companyNetworkId && w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync(cancellationToken);

        List<SubscriberInstallationMaterialPrice> materials = await _context.SubscriberInstallationMaterialPrices
            .Include(m => m.WarehouseLinks)
            .Where(m => m.NetworkId == networkId)
            .ToListAsync(cancellationToken);

        int newLinks = 0;
        List<string> unmatched = [];

        foreach ((string key, string[] terms) in MaterialSearchTerms)
        {
            SubscriberInstallationMaterialPrice? material = materials.FirstOrDefault(m =>
                string.Equals(m.MaterialKey, key, StringComparison.OrdinalIgnoreCase));
            if (material == null)
            {
                continue;
            }

            HashSet<int> existing = material.WarehouseLinks.Select(l => l.WarehouseItemId).ToHashSet();
            List<WarehouseItem> matches = FindAllMatches(warehouseItems, terms, existing);
            if (matches.Count == 0 && existing.Count == 0)
            {
                unmatched.Add(material.MaterialName);
                continue;
            }

            bool hasDefault = material.WarehouseLinks.Any(l => l.IsDefault) || material.WarehouseItemId.HasValue;
            foreach (WarehouseItem match in matches)
            {
                bool makeDefault = !hasDefault;
                material.WarehouseLinks.Add(new SubscriberInstallationMaterialWarehouseLink
                {
                    WarehouseItemId = match.Id,
                    IsDefault = makeDefault,
                    CreatedAt = DateTime.Now
                });
                if (makeDefault)
                {
                    material.WarehouseItemId = match.Id;
                    hasDefault = true;
                }

                newLinks++;
            }

            material.UpdatedAt = DateTime.Now;
        }

        if (newLinks > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new AutoLinkWarehouseResult
        {
            LinkedCount = newLinks,
            UnmatchedMaterialNames = unmatched
        };
    }

    public async Task SyncMaterialWarehouseLinksAsync(
        SubscriberInstallationMaterialPrice material,
        IReadOnlyList<int> warehouseItemIds,
        int? defaultWarehouseItemId,
        CancellationToken cancellationToken = default)
    {
        List<int> ids = warehouseItemIds.Where(id => id > 0).Distinct().ToList();
        int? defaultId = defaultWarehouseItemId is > 0 && ids.Contains(defaultWarehouseItemId.Value)
            ? defaultWarehouseItemId
            : ids.FirstOrDefault();

        List<SubscriberInstallationMaterialWarehouseLink> existing = await _context.SubscriberInstallationMaterialWarehouseLinks
            .Where(l => l.MaterialPriceId == material.Id)
            .ToListAsync(cancellationToken);
        _context.SubscriberInstallationMaterialWarehouseLinks.RemoveRange(existing);

        foreach (int whId in ids)
        {
            _context.SubscriberInstallationMaterialWarehouseLinks.Add(new SubscriberInstallationMaterialWarehouseLink
            {
                MaterialPriceId = material.Id,
                WarehouseItemId = whId,
                IsDefault = defaultId == whId,
                CreatedAt = DateTime.Now
            });
        }

        material.WarehouseItemId = defaultId > 0 ? defaultId : null;
        material.UpdatedAt = DateTime.Now;
    }

    public async Task<IReadOnlyList<WarehouseModelOption>> GetModelsForMaterialAsync(
        int networkId,
        string materialKey,
        CancellationToken cancellationToken = default)
    {
        SubscriberInstallationMaterialPrice? material = await _context.SubscriberInstallationMaterialPrices
            .AsNoTracking()
            .Include(m => m.WarehouseLinks)
            .ThenInclude(l => l.WarehouseItem)
            .FirstOrDefaultAsync(m => m.NetworkId == networkId && m.MaterialKey == materialKey, cancellationToken);
        if (material == null)
        {
            return Array.Empty<WarehouseModelOption>();
        }

        IReadOnlyDictionary<int, decimal> onHand = await GetOnHandByWarehouseItemIdAsync(networkId, cancellationToken);
        return material.WarehouseLinks
            .Where(l => l.WarehouseItem != null)
            .OrderByDescending(l => l.IsDefault)
            .ThenBy(l => l.WarehouseItem!.Name)
            .Select(l => new WarehouseModelOption
            {
                WarehouseItemId = l.WarehouseItemId,
                Name = l.WarehouseItem!.Name,
                ModelNumber = l.WarehouseItem.ModelNumber,
                Sku = l.WarehouseItem.Sku,
                OnHand = onHand.GetValueOrDefault(l.WarehouseItemId, 0m),
                IsDefault = l.IsDefault
            })
            .ToList();
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetOnHandByWarehouseItemIdAsync(
        int networkId,
        CancellationToken cancellationToken = default)
    {
        int companyNetworkId = await ResolveCompanyNetworkIdAsync(networkId, cancellationToken);
        return await _warehouseStock.GetOnHandByItemIdAsync(companyNetworkId, cancellationToken);
    }

    public async Task<PricingWarehouseReadiness> GetReadinessAsync(int networkId, CancellationToken cancellationToken = default)
    {
        List<SubscriberInstallationMaterialPrice> materials = await _context.SubscriberInstallationMaterialPrices
            .AsNoTracking()
            .Include(m => m.WarehouseLinks)
            .Where(m => m.NetworkId == networkId && m.IsActive)
            .ToListAsync(cancellationToken);

        List<SubscriberInstallationMaterialPrice> stockMaterials = materials
            .Where(m => IsStockMaterialKey(m.MaterialKey))
            .ToList();

        int unlinked = stockMaterials.Count(m => !HasAnyWarehouseLink(m));
        return new PricingWarehouseReadiness
        {
            ActiveStockLineCount = stockMaterials.Count,
            UnlinkedStockLineCount = unlinked,
            IsReadyForWarehouseFinalize = unlinked == 0 && stockMaterials.Count > 0
        };
    }

    public static bool HasAnyWarehouseLink(SubscriberInstallationMaterialPrice material) =>
        material.WarehouseItemId.HasValue || material.WarehouseLinks.Count > 0;

    public static int? ResolveDefaultWarehouseItemId(SubscriberInstallationMaterialPrice material)
    {
        SubscriberInstallationMaterialWarehouseLink? defaultLink =
            material.WarehouseLinks.FirstOrDefault(l => l.IsDefault);
        return defaultLink?.WarehouseItemId ?? material.WarehouseItemId;
    }

    public static string FormatWarehouseItemLabel(WarehouseItem item, decimal onHand) =>
        $"{item.Name}{(string.IsNullOrWhiteSpace(item.ModelNumber) ? "" : $" — موديل {item.ModelNumber}")}{(string.IsNullOrWhiteSpace(item.Sku) ? "" : $" [{item.Sku}]")} (متاح: {onHand:0.##})";

    private static List<WarehouseItem> FindAllMatches(
        IReadOnlyList<WarehouseItem> items,
        string[] searchTerms,
        HashSet<int> excludeIds)
    {
        List<(WarehouseItem item, int score)> scored = [];
        foreach (WarehouseItem item in items)
        {
            if (excludeIds.Contains(item.Id))
            {
                continue;
            }

            int score = ScoreItem(item, searchTerms);
            if (score > 0)
            {
                scored.Add((item, score));
            }
        }

        return scored
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.item.Name)
            .Select(x => x.item)
            .ToList();
    }

    private static int ScoreItem(WarehouseItem item, string[] searchTerms)
    {
        int score = 0;
        string name = item.Name.Trim();
        foreach (string term in searchTerms)
        {
            if (name.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += term.Length >= 4 ? 10 : 5;
            }

            if (!string.IsNullOrWhiteSpace(item.ModelNumber) &&
                item.ModelNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 6;
            }

            if (!string.IsNullOrWhiteSpace(item.Sku) &&
                item.Sku.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 3;
            }
        }

        return score;
    }

    private async Task<int> ResolveCompanyNetworkIdAsync(int networkId, CancellationToken cancellationToken)
    {
        int? parentId = await _context.Networks
            .AsNoTracking()
            .Where(n => n.Id == networkId)
            .Select(n => n.ParentNetworkId)
            .FirstOrDefaultAsync(cancellationToken);
        return parentId ?? networkId;
    }
}

public sealed class WarehouseModelOption
{
    public int WarehouseItemId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? ModelNumber { get; init; }
    public string? Sku { get; init; }
    public decimal OnHand { get; init; }
    public bool IsDefault { get; init; }

    public string DisplayLabel =>
        $"{Name}{(string.IsNullOrWhiteSpace(ModelNumber) ? "" : $" — {ModelNumber}")} (متاح: {OnHand:0.##})";
}

public sealed class AutoLinkWarehouseResult
{
    public int LinkedCount { get; init; }
    public IReadOnlyList<string> UnmatchedMaterialNames { get; init; } = Array.Empty<string>();
}

public sealed class PricingWarehouseReadiness
{
    public int ActiveStockLineCount { get; init; }
    public int UnlinkedStockLineCount { get; init; }
    public bool IsReadyForWarehouseFinalize { get; init; }
}
