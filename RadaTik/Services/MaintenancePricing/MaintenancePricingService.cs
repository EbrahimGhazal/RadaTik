using Microsoft.EntityFrameworkCore;
using RadaTik.Areas.CompanyAdmin.ViewModels;
using RadaTik.Data;
using RadaTik.Models;
using RadaTik.ViewModels.Maintenance;

namespace RadaTik.Services.MaintenancePricing;

public sealed class MaintenancePricingOperationResult
{
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public int AffectedCount { get; init; }

    public static MaintenancePricingOperationResult Ok(int affectedCount = 0) => new() { Success = true, AffectedCount = affectedCount };
    public static MaintenancePricingOperationResult Fail(string message) => new() { Success = false, ErrorMessage = message };
}

public sealed class MaintenancePricingScopeContext
{
    public required int MainNetworkId { get; init; }
    public required int CurrentNetworkId { get; init; }
    public required bool CanUseCurrentScope { get; init; }
}

public interface IMaintenancePricingScopeStrategy
{
    string ScopeKey { get; }
    bool IsAvailable(MaintenancePricingScopeContext context);
    int ResolveTargetNetworkId(MaintenancePricingScopeContext context);
}

internal sealed class MainMaintenancePricingScopeStrategy : IMaintenancePricingScopeStrategy
{
    public string ScopeKey => "main";
    public bool IsAvailable(MaintenancePricingScopeContext context) => true;
    public int ResolveTargetNetworkId(MaintenancePricingScopeContext context) => context.MainNetworkId;
}

internal sealed class CurrentMaintenancePricingScopeStrategy : IMaintenancePricingScopeStrategy
{
    public string ScopeKey => "current";
    public bool IsAvailable(MaintenancePricingScopeContext context) => context.CanUseCurrentScope;
    public int ResolveTargetNetworkId(MaintenancePricingScopeContext context) => context.CurrentNetworkId;
}

public interface IMaintenancePricingService
{
    Task<MaintenancePricingPageViewModel?> BuildPageModelAsync(int selectedNetworkId, string? networkScope, CancellationToken ct = default);
    Task<MaintenancePricingOperationResult> SaveRowsAsync(int selectedNetworkId, string? networkScope, IReadOnlyCollection<MaintenancePricingBulkSaveRowInput> rows, string actorUserId, CancellationToken ct = default);
    Task<MaintenancePricingOperationResult> SaveSingleAsync(int selectedNetworkId, string? networkScope, MaintenanceType maintenanceType, decimal amountSyp, bool isActive, string actorUserId, CancellationToken ct = default);
    Task<MaintenancePricingOperationResult> CopyFromOtherScopeAsync(int selectedNetworkId, string? networkScope, string actorUserId, CancellationToken ct = default);
    Task<List<PricedMaintenanceOptionViewModel>> LoadPricedSolutionOptionsAsync(int clientNetworkId, CancellationToken ct = default);
    string NormalizeScope(string? networkScope);
}

public sealed class MaintenancePricingService : IMaintenancePricingService
{
    private sealed record NetworkIdParentRow(int Id, int? ParentNetworkId);

    private readonly ApplicationDbContext _db;
    private readonly IReadOnlyDictionary<string, IMaintenancePricingScopeStrategy> _scopeStrategies;

    public MaintenancePricingService(ApplicationDbContext db, IEnumerable<IMaintenancePricingScopeStrategy> scopeStrategies)
    {
        _db = db;
        _scopeStrategies = scopeStrategies.ToDictionary(x => x.ScopeKey, StringComparer.OrdinalIgnoreCase);
    }

    public string NormalizeScope(string? networkScope)
        => string.Equals(networkScope, "current", StringComparison.OrdinalIgnoreCase) ? "current" : "main";

    public async Task<MaintenancePricingPageViewModel?> BuildPageModelAsync(int selectedNetworkId, string? networkScope, CancellationToken ct = default)
    {
        NetworkIdParentRow? selected = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == selectedNetworkId)
            .Select(n => new NetworkIdParentRow(n.Id, n.ParentNetworkId))
            .FirstOrDefaultAsync(ct);
        if (selected == null)
        {
            return null;
        }

        MaintenancePricingScopeContext context = BuildScopeContext(selected.Id, selected.ParentNetworkId);
        string scope = NormalizeScope(networkScope);
        IMaintenancePricingScopeStrategy targetScope = ResolveScopeStrategy(scope, context);
        int targetNetworkId = targetScope.ResolveTargetNetworkId(context);

        string targetNetworkName = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == targetNetworkId)
            .Select(n => n.Name)
            .FirstOrDefaultAsync(ct) ?? $"#{targetNetworkId}";

        List<NetworkMaintenancePrice> prices = await _db.NetworkMaintenancePrices
            .AsNoTracking()
            .Where(x => x.NetworkId == targetNetworkId)
            .ToListAsync(ct);

        List<MaintenancePricingRowViewModel> rows = MaintenanceCatalog.SolutionTypes
            .Select(type =>
            {
                NetworkMaintenancePrice? row = prices.FirstOrDefault(x => x.MaintenanceType == type);
                return new MaintenancePricingRowViewModel
                {
                    Type = type,
                    SolutionName = MaintenanceCatalog.GetDisplayName(type),
                    AmountSyp = row?.AmountSYP ?? 0m,
                    IsActive = row?.IsActive ?? false
                };
            })
            .ToList();

        return new MaintenancePricingPageViewModel
        {
            NetworkId = targetNetworkId,
            NetworkScope = targetScope.ScopeKey,
            EffectiveNetworkName = targetNetworkName,
            CanUseCurrentNetworkScope = context.CanUseCurrentScope,
            Rows = rows
        };
    }

    public Task<MaintenancePricingOperationResult> SaveSingleAsync(int selectedNetworkId, string? networkScope, MaintenanceType maintenanceType, decimal amountSyp, bool isActive, string actorUserId, CancellationToken ct = default)
    {
        List<MaintenancePricingBulkSaveRowInput> rows = new List<MaintenancePricingBulkSaveRowInput>
        {
            new()
            {
                Type = maintenanceType,
                AmountSyp = amountSyp,
                IsActive = isActive
            }
        };
        return SaveRowsAsync(selectedNetworkId, networkScope, rows, actorUserId, ct);
    }

    public async Task<MaintenancePricingOperationResult> SaveRowsAsync(int selectedNetworkId, string? networkScope, IReadOnlyCollection<MaintenancePricingBulkSaveRowInput> rows, string actorUserId, CancellationToken ct = default)
    {
        if (rows.Count == 0)
        {
            return MaintenancePricingOperationResult.Fail("لا توجد بيانات للحفظ.");
        }
        if (rows.Any(r => r.AmountSyp < 0m))
        {
            return MaintenancePricingOperationResult.Fail("جميع الأسعار يجب أن تكون صفر أو أكبر.");
        }
        if (rows.Any(r => !MaintenanceCatalog.IsSolutionType(r.Type)))
        {
            return MaintenancePricingOperationResult.Fail("لا يمكن حفظ تسعير لأنواع أعطال. التسعير يطبق فقط على طرق الحل.");
        }

        MaintenancePricingScopeContext? context = await ResolveScopeContextAsync(selectedNetworkId, ct);
        if (context == null)
        {
            return MaintenancePricingOperationResult.Fail("تعذر تحديد الشبكة الحالية.");
        }

        IMaintenancePricingScopeStrategy targetScope = ResolveScopeStrategy(NormalizeScope(networkScope), context);
        int targetNetworkId = targetScope.ResolveTargetNetworkId(context);
        List<MaintenanceType> targetTypes = rows.Select(r => r.Type).Distinct().ToList();

        Dictionary<MaintenanceType, NetworkMaintenancePrice> existingByType = await _db.NetworkMaintenancePrices
            .Where(x => x.NetworkId == targetNetworkId && targetTypes.Contains(x.MaintenanceType))
            .ToDictionaryAsync(x => x.MaintenanceType, ct);

        DateTime now = DateTime.Now;
        int affected = 0;
        foreach (MaintenancePricingBulkSaveRowInput row in rows)
        {
            if (!existingByType.TryGetValue(row.Type, out NetworkMaintenancePrice? existing))
            {
                existing = new NetworkMaintenancePrice
                {
                    NetworkId = targetNetworkId,
                    MaintenanceType = row.Type
                };
                _db.NetworkMaintenancePrices.Add(existing);
                existingByType[row.Type] = existing;
            }

            existing.AmountSYP = row.AmountSyp;
            existing.IsActive = row.IsActive;
            existing.UpdatedByUserId = actorUserId;
            existing.UpdatedAt = now;
            affected++;
        }

        await _db.SaveChangesAsync(ct);
        return MaintenancePricingOperationResult.Ok(affected);
    }

    public async Task<MaintenancePricingOperationResult> CopyFromOtherScopeAsync(int selectedNetworkId, string? networkScope, string actorUserId, CancellationToken ct = default)
    {
        MaintenancePricingScopeContext? context = await ResolveScopeContextAsync(selectedNetworkId, ct);
        if (context == null)
        {
            return MaintenancePricingOperationResult.Fail("تعذر تحديد الشبكة الحالية.");
        }

        IMaintenancePricingScopeStrategy targetScope = ResolveScopeStrategy(NormalizeScope(networkScope), context);
        IMaintenancePricingScopeStrategy sourceScope = ResolveScopeStrategy(
            string.Equals(targetScope.ScopeKey, "main", StringComparison.OrdinalIgnoreCase) ? "current" : "main",
            context);

        if (!sourceScope.IsAvailable(context))
        {
            return MaintenancePricingOperationResult.Fail("لا يوجد نطاق آخر للنسخ منه ضمن الشبكة الحالية.");
        }

        int targetNetworkId = targetScope.ResolveTargetNetworkId(context);
        int sourceNetworkId = sourceScope.ResolveTargetNetworkId(context);
        if (targetNetworkId == sourceNetworkId)
        {
            return MaintenancePricingOperationResult.Fail("لا يوجد نطاق آخر للنسخ منه ضمن الشبكة الحالية.");
        }

        List<MaintenanceType> targetTypes = MaintenanceCatalog.SolutionTypes.ToList();
        Dictionary<MaintenanceType, NetworkMaintenancePrice> sourceByType = await _db.NetworkMaintenancePrices
            .AsNoTracking()
            .Where(x => x.NetworkId == sourceNetworkId && targetTypes.Contains(x.MaintenanceType))
            .GroupBy(x => x.MaintenanceType)
            .ToDictionaryAsync(g => g.Key, g => g.OrderByDescending(x => x.Id).First(), ct);

        if (sourceByType.Count == 0)
        {
            return MaintenancePricingOperationResult.Fail("لا توجد أسعار في النطاق المصدر لنسخها.");
        }

        Dictionary<MaintenanceType, NetworkMaintenancePrice> targetExisting = await _db.NetworkMaintenancePrices
            .Where(x => x.NetworkId == targetNetworkId && targetTypes.Contains(x.MaintenanceType))
            .ToDictionaryAsync(x => x.MaintenanceType, ct);

        DateTime now = DateTime.Now;
        int copiedCount = 0;
        foreach (MaintenanceType type in targetTypes)
        {
            if (!sourceByType.TryGetValue(type, out NetworkMaintenancePrice? source))
            {
                continue;
            }

            if (!targetExisting.TryGetValue(type, out NetworkMaintenancePrice? target))
            {
                target = new NetworkMaintenancePrice
                {
                    NetworkId = targetNetworkId,
                    MaintenanceType = type
                };
                _db.NetworkMaintenancePrices.Add(target);
                targetExisting[type] = target;
            }

            target.AmountSYP = source.AmountSYP;
            target.IsActive = source.IsActive;
            target.UpdatedByUserId = actorUserId;
            target.UpdatedAt = now;
            copiedCount++;
        }

        await _db.SaveChangesAsync(ct);
        return MaintenancePricingOperationResult.Ok(copiedCount);
    }

    public async Task<List<PricedMaintenanceOptionViewModel>> LoadPricedSolutionOptionsAsync(int clientNetworkId, CancellationToken ct = default)
    {
        MaintenancePricingScopeContext? context = await ResolveScopeContextAsync(clientNetworkId, ct);
        if (context == null)
        {
            return [];
        }

        List<IMaintenancePricingScopeStrategy> orderedScopes = new List<IMaintenancePricingScopeStrategy> { _scopeStrategies["main"] };
        if (context.CanUseCurrentScope)
        {
            orderedScopes.Add(_scopeStrategies["current"]);
        }

        foreach (IMaintenancePricingScopeStrategy scope in orderedScopes)
        {
            if (!scope.IsAvailable(context))
            {
                continue;
            }

            int networkId = scope.ResolveTargetNetworkId(context);
            List<NetworkMaintenancePrice> rows = await _db.NetworkMaintenancePrices
                .AsNoTracking()
                .Where(p => p.NetworkId == networkId)
                .OrderByDescending(p => p.Id)
                .ToListAsync(ct);

            if (rows.Count == 0)
            {
                continue;
            }

            List<NetworkMaintenancePrice> distinctByType = rows
                .GroupBy(p => p.MaintenanceType)
                .Select(g => g.First())
                .Where(p => p.IsActive && MaintenanceCatalog.IsSolutionType(p.MaintenanceType))
                .OrderBy(p => MaintenanceCatalog.GetOrder(p.MaintenanceType))
                .ToList();

            if (distinctByType.Count == 0)
            {
                continue;
            }

            return distinctByType.Select(p => new PricedMaintenanceOptionViewModel
            {
                MaintenanceType = p.MaintenanceType,
                DisplayName = MaintenanceCatalog.GetDisplayName(p.MaintenanceType),
                AmountSYP = p.AmountSYP,
                IsDefaultForRequestType = false
            }).ToList();
        }

        return [];
    }

    private IMaintenancePricingScopeStrategy ResolveScopeStrategy(string requestedScope, MaintenancePricingScopeContext context)
    {
        if (!_scopeStrategies.TryGetValue(requestedScope, out IMaintenancePricingScopeStrategy? strategy) || !strategy.IsAvailable(context))
        {
            IMaintenancePricingScopeStrategy fallback = _scopeStrategies["main"];
            return fallback;
        }
        return strategy;
    }

    private async Task<MaintenancePricingScopeContext?> ResolveScopeContextAsync(int selectedNetworkId, CancellationToken ct)
    {
        NetworkIdParentRow? selected = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == selectedNetworkId)
            .Select(n => new NetworkIdParentRow(n.Id, n.ParentNetworkId))
            .FirstOrDefaultAsync(ct);
        return selected == null ? null : BuildScopeContext(selected.Id, selected.ParentNetworkId);
    }

    private static MaintenancePricingScopeContext BuildScopeContext(int currentNetworkId, int? parentNetworkId)
    {
        int mainNetworkId = parentNetworkId ?? currentNetworkId;
        return new MaintenancePricingScopeContext
        {
            MainNetworkId = mainNetworkId,
            CurrentNetworkId = currentNetworkId,
            CanUseCurrentScope = parentNetworkId.HasValue
        };
    }
}
