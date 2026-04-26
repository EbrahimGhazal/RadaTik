using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Services.SystemAdminPricing;

public sealed class RecurringServiceSnapshot
{
    public string ServiceName { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal InitialPriceSyp { get; init; }
    public PricingBillingPeriod RenewalBillingPeriod { get; init; } = PricingBillingPeriod.Monthly;
    public decimal RenewalPricePerUnitSyp { get; init; }
    public bool HasInitialPricing { get; init; }
    public bool HasRenewalPricing { get; init; }
    public int FreeInitialUnits { get; init; }
    public int FreeRenewalUnits { get; init; }
}

public sealed class ReportServiceSnapshot
{
    public decimal InitialPriceSyp { get; init; }
    public bool HasInitialPricing { get; init; }
}

public sealed class ServiceCatalogSnapshot
{
    public List<SystemService> Items { get; init; } = [];
    public RecurringServiceSnapshot NetworkPricing { get; init; } = new();
    public RecurringServiceSnapshot ServerPricing { get; init; } = new();
    public RecurringServiceSnapshot SectorPricing { get; init; } = new();
    public RecurringServiceSnapshot ReceiverPricing { get; init; } = new();
    public RecurringServiceSnapshot ClientPricing { get; init; } = new();
    public RecurringServiceSnapshot UserPricing { get; init; } = new();
    public RecurringServiceSnapshot SpeedProfilePricing { get; init; } = new();
    public ReportServiceSnapshot ReportPricing { get; init; } = new();
    public decimal ProfilePriceTax { get; init; }
    public MaintenanceCommissionMode MaintenanceCommissionMode { get; init; } = MaintenanceCommissionMode.Fixed;
    public decimal MaintenanceCommissionValue { get; init; }
}

public interface IServiceCatalogSnapshotProvider
{
    Task<ServiceCatalogSnapshot> BuildAsync(CancellationToken ct = default);
}

public sealed class ServiceCatalogSnapshotProvider : IServiceCatalogSnapshotProvider
{
    private readonly ApplicationDbContext _db;

    public ServiceCatalogSnapshotProvider(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceCatalogSnapshot> BuildAsync(CancellationToken ct = default)
    {
        var items = await _db.SystemServices
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        var network = await LoadRecurringAsync(FeatureKeys.Networks, PricingChargeUnit.PerNetwork, "إدارة الشبكة", "إنشاء وإدارة الشبكات التابعة لمدير الشركة.", ct);
        var server = await LoadRecurringAsync(FeatureKeys.MikroTikServers, PricingChargeUnit.PerServer, "خوادم MikroTik", "إضافة وإدارة خوادم MikroTik.", ct);
        var sector = await LoadRecurringAsync(FeatureKeys.Sectors, PricingChargeUnit.PerSector, "القطاعات (المرسلات)", "إضافة وإدارة القطاعات/المرسلات.", ct);
        var receiver = await LoadRecurringAsync(FeatureKeys.Receivers, PricingChargeUnit.PerReceiver, "اللاقطات/المستقبلات", "إضافة وإدارة اللاقطات (المستقبلات) ضمن القطاعات.", ct);
        var client = await LoadRecurringAsync(FeatureKeys.Clients, PricingChargeUnit.PerSubscriber, "المشتركين/العملاء", "إضافة وإدارة المشتركين (العملاء).", ct);
        var user = await LoadRecurringAsync(FeatureKeys.Users, PricingChargeUnit.PerUser, "الموظفين/المستخدمين", "إضافة وإدارة الموظفين والمستخدمين.", ct);
        var speed = await LoadRecurringAsync(FeatureKeys.Profiles, PricingChargeUnit.PerSpeedProfile, "السرعة/البروفايل", "إضافة/استيراد وإدارة بروفايلات السرعة.", ct);

        var reportPricing = await GetLatestActivePricingAsync(
            FeatureKeys.ReportsExport,
            PricingChargeUnit.PerReport,
            PricingBillingPeriod.OneTime,
            ct);

        var profileTax = await GetLatestActivePricingAsync(
            FeatureKeys.ProfilePriceTax,
            PricingChargeUnit.Flat,
            PricingBillingPeriod.OneTime,
            ct);

        var maintenanceCommission = await GetLatestActivePricingAsync(
            FeatureKeys.MaintenanceCommission,
            null,
            PricingBillingPeriod.OneTime,
            ct);

        return new ServiceCatalogSnapshot
        {
            Items = items,
            NetworkPricing = network,
            ServerPricing = server,
            SectorPricing = sector,
            ReceiverPricing = receiver,
            ClientPricing = client,
            UserPricing = user,
            SpeedProfilePricing = speed,
            ReportPricing = new ReportServiceSnapshot
            {
                InitialPriceSyp = reportPricing?.AmountSYP ?? 0m,
                HasInitialPricing = reportPricing != null
            },
            ProfilePriceTax = profileTax?.AmountSYP ?? 15m,
            MaintenanceCommissionMode = maintenanceCommission?.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount
                ? MaintenanceCommissionMode.Percent
                : MaintenanceCommissionMode.Fixed,
            MaintenanceCommissionValue = maintenanceCommission?.AmountSYP ?? 0m
        };
    }

    private async Task<RecurringServiceSnapshot> LoadRecurringAsync(
        string featureKey,
        PricingChargeUnit chargeUnit,
        string fallbackName,
        string fallbackDescription,
        CancellationToken ct)
    {
        var feature = FeatureCatalog.All.FirstOrDefault(f => f.Key == featureKey);
        var rows = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == featureKey &&
                p.ChargeUnit == chargeUnit)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        var initial = rows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
        var renewal = rows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
        var policy = RecurringPricingPolicyCodec.ReadFromNotes(initial?.Notes, renewal?.Notes);

        return new RecurringServiceSnapshot
        {
            ServiceName = feature?.DisplayName ?? fallbackName,
            Description = feature?.Description ?? fallbackDescription,
            InitialPriceSyp = initial?.AmountSYP ?? 0m,
            RenewalBillingPeriod = renewal?.BillingPeriod ?? PricingBillingPeriod.Monthly,
            RenewalPricePerUnitSyp = renewal?.AmountSYP ?? 0m,
            HasInitialPricing = initial != null,
            HasRenewalPricing = renewal != null,
            FreeInitialUnits = policy.FreeInitialUnits,
            FreeRenewalUnits = policy.FreeRenewalUnits
        };
    }

    private Task<FeaturePricing?> GetLatestActivePricingAsync(
        string featureKey,
        PricingChargeUnit? chargeUnit,
        PricingBillingPeriod billingPeriod,
        CancellationToken ct)
    {
        var query = _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == featureKey &&
                p.BillingPeriod == billingPeriod);

        if (chargeUnit.HasValue)
        {
            query = query.Where(p => p.ChargeUnit == chargeUnit.Value);
        }

        return query
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }
}
