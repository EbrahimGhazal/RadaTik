using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.SystemAdminPricing;

namespace RadaTik.Services.PricingPreview;

public static class PricingPreviewCounterKeys
{
    public const string Networks = "networks";
    public const string Clients = "clients";
    public const string Receivers = "receivers";
    public const string Sectors = "sectors";
    public const string Profiles = "profiles";
    public const string MikroTikServers = "mikrotik-servers";
    public const string Employees = "employees";
}

public sealed class CreatePricingPreviewResult
{
    public bool HasInitialPricing { get; init; }
    public bool HasRenewalPricing { get; init; }
    public decimal InitialPriceSyp { get; init; }
    public decimal RenewalPriceSyp { get; init; }
    public string RenewalPeriodLabel { get; init; } = "غير محدد";
    public int FreeInitialUnits { get; init; }
    public int FreeRenewalUnits { get; init; }
    public bool ShouldChargeNow { get; init; }
    public string CompanyName { get; init; } = "غير محدد";
    public int TotalUnits { get; init; }
}

public interface IPricingPreviewUnitsCounterStrategy
{
    string Key { get; }
    Task<int> CountAsync(ApplicationDbContext db, IReadOnlyCollection<int> companyScopeNetworkIds, CancellationToken ct = default);
}

public interface ICreatePricingPreviewService
{
    Task<CreatePricingPreviewResult> BuildAsync(
        int selectedNetworkId,
        string featureKey,
        PricingChargeUnit chargeUnit,
        string counterKey,
        CancellationToken ct = default);
}

internal abstract class ScopeNetworkEntityCounterStrategyBase : IPricingPreviewUnitsCounterStrategy
{
    public abstract string Key { get; }
    protected abstract IQueryable<int?> QueryNetworkIds(ApplicationDbContext db);

    public async Task<int> CountAsync(ApplicationDbContext db, IReadOnlyCollection<int> companyScopeNetworkIds, CancellationToken ct = default)
    {
        if (companyScopeNetworkIds.Count == 0)
        {
            return 0;
        }

        return await QueryNetworkIds(db)
            .Where(nid => nid.HasValue && companyScopeNetworkIds.Contains(nid.Value))
            .CountAsync(ct);
    }
}

internal sealed class ClientsUnitsCounterStrategy : ScopeNetworkEntityCounterStrategyBase
{
    public override string Key => PricingPreviewCounterKeys.Clients;
    protected override IQueryable<int?> QueryNetworkIds(ApplicationDbContext db) => db.Clients.AsNoTracking().Select(x => x.NetworkId);
}

internal sealed class NetworksUnitsCounterStrategy : ScopeNetworkEntityCounterStrategyBase
{
    public override string Key => PricingPreviewCounterKeys.Networks;
    protected override IQueryable<int?> QueryNetworkIds(ApplicationDbContext db) => db.Networks.AsNoTracking().Select(x => (int?)x.Id);
}

internal sealed class ReceiversUnitsCounterStrategy : ScopeNetworkEntityCounterStrategyBase
{
    public override string Key => PricingPreviewCounterKeys.Receivers;
    protected override IQueryable<int?> QueryNetworkIds(ApplicationDbContext db) => db.Receivers.AsNoTracking().Select(x => x.NetworkId);
}

internal sealed class SectorsUnitsCounterStrategy : ScopeNetworkEntityCounterStrategyBase
{
    public override string Key => PricingPreviewCounterKeys.Sectors;
    protected override IQueryable<int?> QueryNetworkIds(ApplicationDbContext db) => db.Sectors.AsNoTracking().Select(x => x.NetworkId);
}

internal sealed class ProfilesUnitsCounterStrategy : ScopeNetworkEntityCounterStrategyBase
{
    public override string Key => PricingPreviewCounterKeys.Profiles;
    protected override IQueryable<int?> QueryNetworkIds(ApplicationDbContext db) => db.Profiles.AsNoTracking().Select(x => x.NetworkId);
}

internal sealed class MikroTikServersUnitsCounterStrategy : ScopeNetworkEntityCounterStrategyBase
{
    public override string Key => PricingPreviewCounterKeys.MikroTikServers;
    protected override IQueryable<int?> QueryNetworkIds(ApplicationDbContext db) => db.MikroTikServers.AsNoTracking().Where(x => x.IsActive).Select(x => x.NetworkId);
}

internal sealed class EmployeesUnitsCounterStrategy : IPricingPreviewUnitsCounterStrategy
{
    public string Key => PricingPreviewCounterKeys.Employees;

    public async Task<int> CountAsync(ApplicationDbContext db, IReadOnlyCollection<int> companyScopeNetworkIds, CancellationToken ct = default)
    {
        if (companyScopeNetworkIds.Count == 0)
        {
            return 0;
        }

        List<string> employeeRoleIds = await db.Roles
            .AsNoTracking()
            .Where(r => r.Name == RoleNames.CompanyEmployee || r.Name == RoleNames.EmployeeLegacy)
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (employeeRoleIds.Count == 0)
        {
            return 0;
        }

        return await db.Users
            .AsNoTracking()
            .Where(u =>
                u.NetworkId.HasValue &&
                companyScopeNetworkIds.Contains(u.NetworkId.Value) &&
                db.UserRoles.Any(ur => ur.UserId == u.Id && employeeRoleIds.Contains(ur.RoleId)))
            .CountAsync(ct);
    }
}

public sealed class CreatePricingPreviewService : ICreatePricingPreviewService
{
    private readonly ApplicationDbContext _db;
    private readonly IReadOnlyDictionary<string, IPricingPreviewUnitsCounterStrategy> _strategies;

    public CreatePricingPreviewService(
        ApplicationDbContext db,
        IEnumerable<IPricingPreviewUnitsCounterStrategy> strategies)
    {
        _db = db;
        _strategies = strategies.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<CreatePricingPreviewResult> BuildAsync(
        int selectedNetworkId,
        string featureKey,
        PricingChargeUnit chargeUnit,
        string counterKey,
        CancellationToken ct = default)
    {
        Network? selectedNetwork = await _db.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == selectedNetworkId, ct);
        int companyNetworkId = selectedNetwork?.ParentNetworkId ?? selectedNetworkId;
        List<int> companyScope = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);
        string? companyName = await _db.Networks
            .AsNoTracking()
            .Where(n => n.Id == companyNetworkId)
            .Select(n => n.Name)
            .FirstOrDefaultAsync(ct);

        List<FeaturePricing> pricingRows = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == featureKey &&
                p.ChargeUnit == chargeUnit)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        FeaturePricing? initialPricing = pricingRows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
        FeaturePricing? renewalPricing = pricingRows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
        RecurringPricingPolicy recurringPolicy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, renewalPricing);

        if (!_strategies.TryGetValue(counterKey, out IPricingPreviewUnitsCounterStrategy? strategy))
        {
            throw new InvalidOperationException($"Unknown pricing counter strategy key: {counterKey}");
        }

        int totalUnits = await strategy.CountAsync(_db, companyScope, ct);

        return new CreatePricingPreviewResult
        {
            HasInitialPricing = initialPricing != null,
            HasRenewalPricing = renewalPricing != null,
            InitialPriceSyp = initialPricing != null ? WalletMath.CeilSyp(initialPricing.AmountSYP) : 0m,
            RenewalPriceSyp = renewalPricing != null ? WalletMath.CeilSyp(renewalPricing.AmountSYP) : 0m,
            RenewalPeriodLabel = renewalPricing != null
                ? PricingDisplay.BillingPeriodLabel(renewalPricing.BillingPeriod)
                : "غير محدد",
            FreeInitialUnits = recurringPolicy.FreeInitialUnits,
            FreeRenewalUnits = recurringPolicy.FreeRenewalUnits,
            ShouldChargeNow = totalUnits >= recurringPolicy.FreeInitialUnits,
            CompanyName = companyName ?? selectedNetwork?.Name ?? "غير محدد",
            TotalUnits = totalUnits
        };
    }
}
