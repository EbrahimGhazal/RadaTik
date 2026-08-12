using Microsoft.EntityFrameworkCore;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;

namespace RadaTik.Services.SystemAdminPricing;

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

public sealed class ServiceCatalogDocumentationRow
{
    public string FeatureKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string? DetailHtml { get; init; }
    public string? PricingPolicyHtml { get; init; }
    public string? RenewalPolicyHtml { get; init; }
    public string PricingPlansSummaryHtml { get; init; } = string.Empty;
    public string? SuggestedRenewalPolicyHtml { get; init; }
}

public sealed class ServiceCatalogSnapshot
{
    public List<ServiceCatalogDocumentationRow> Documentation { get; set; } = [];
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
    public bool HasProfilePriceTax { get; init; }
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
        List<SystemService> items = await _db.SystemServices
            .AsNoTracking()
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

        RecurringServiceSnapshot network = await LoadRecurringAsync(FeatureKeys.Networks, PricingChargeUnit.PerNetwork, "إدارة الشبكة", "إنشاء وإدارة الشبكات التابعة لمدير الشركة.", ct);
        RecurringServiceSnapshot server = await LoadRecurringAsync(FeatureKeys.MikroTikServers, PricingChargeUnit.PerServer, "خوادم MikroTik", "إضافة وإدارة خوادم MikroTik.", ct);
        RecurringServiceSnapshot sector = await LoadRecurringAsync(FeatureKeys.Sectors, PricingChargeUnit.PerSector, "القطاعات (المرسلات)", "إضافة وإدارة القطاعات/المرسلات.", ct);
        RecurringServiceSnapshot receiver = await LoadRecurringAsync(FeatureKeys.Receivers, PricingChargeUnit.PerReceiver, "اللاقطات/المستقبلات", "إضافة وإدارة اللاقطات (المستقبلات) ضمن القطاعات.", ct);
        RecurringServiceSnapshot client = await LoadRecurringAsync(FeatureKeys.Clients, PricingChargeUnit.PerSubscriber, "المشتركين/العملاء", "إضافة وإدارة المشتركين (العملاء).", ct);
        RecurringServiceSnapshot user = await LoadRecurringAsync(FeatureKeys.Users, PricingChargeUnit.PerUser, "الموظفين/المستخدمين", "إضافة وإدارة الموظفين والمستخدمين.", ct);
        RecurringServiceSnapshot speed = await LoadRecurringAsync(FeatureKeys.Profiles, PricingChargeUnit.PerSpeedProfile, "السرعة/البروفايل", "إضافة/استيراد وإدارة بروفايلات السرعة.", ct);

        FeaturePricing? reportPricing = await GetLatestActivePricingAsync(
            FeatureKeys.ReportsExport,
            PricingChargeUnit.PerReport,
            PricingBillingPeriod.OneTime,
            ct);

        FeaturePricing? profileTax = await GetLatestActivePricingAsync(
            FeatureKeys.ProfilePriceTax,
            PricingChargeUnit.Flat,
            PricingBillingPeriod.OneTime,
            ct);

        FeaturePricing? maintenanceCommission = await GetLatestActivePricingAsync(
            FeatureKeys.MaintenanceCommission,
            null,
            PricingBillingPeriod.OneTime,
            ct);

        ServiceCatalogSnapshot snapshot = new ServiceCatalogSnapshot
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
            HasProfilePriceTax = profileTax != null,
            MaintenanceCommissionMode = maintenanceCommission?.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount
                ? MaintenanceCommissionMode.Percent
                : MaintenanceCommissionMode.Fixed,
            MaintenanceCommissionValue = maintenanceCommission?.AmountSYP ?? 0m
        };

        snapshot.Documentation = await BuildDocumentationRowsAsync(snapshot, ct);
        return snapshot;
    }

    private async Task<List<ServiceCatalogDocumentationRow>> BuildDocumentationRowsAsync(
        ServiceCatalogSnapshot snapshot,
        CancellationToken ct)
    {
        Dictionary<string, FeaturePublicInfo> publicInfoByKey = await _db.FeaturePublicInfos
            .AsNoTracking()
            .ToDictionaryAsync(f => f.FeatureKey, f => f, StringComparer.OrdinalIgnoreCase, ct);

        List<FeaturePricing> allActivePricings = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        List<ServiceCatalogDocumentationRow> rows = new List<ServiceCatalogDocumentationRow>();
        foreach (FeatureCatalog.FeatureDefinition def in FeatureCatalog.All)
        {
            publicInfoByKey.TryGetValue(def.Key, out FeaturePublicInfo? info);
            List<FeaturePricing> servicePricings = allActivePricings
                .Where(p => string.Equals(p.FeatureKey, def.Key, StringComparison.OrdinalIgnoreCase))
                .ToList();

            RecurringServiceSnapshot? recurring = ServiceCatalogDocumentationHelper.TryMapRecurringSnapshot(def.Key, snapshot);
            string? suggestedRenewal = recurring != null
                ? ServiceCatalogDocumentationHelper.BuildSuggestedRenewalFromRecurring(recurring)
                : null;

            rows.Add(new ServiceCatalogDocumentationRow
            {
                FeatureKey = def.Key,
                DisplayName = def.DisplayName,
                Category = def.Category,
                Description = def.Description,
                DetailHtml = info?.DetailHtml,
                PricingPolicyHtml = info?.PricingPolicyHtml,
                RenewalPolicyHtml = info?.RenewalPolicyHtml,
                PricingPlansSummaryHtml = ServiceCatalogDocumentationHelper.BuildPricingPlansSummaryHtml(servicePricings),
                SuggestedRenewalPolicyHtml = suggestedRenewal
            });
        }

        return rows
            .OrderBy(r => r.Category)
            .ThenBy(r => r.DisplayName)
            .ToList();
    }

    private async Task<RecurringServiceSnapshot> LoadRecurringAsync(
        string featureKey,
        PricingChargeUnit chargeUnit,
        string fallbackName,
        string fallbackDescription,
        CancellationToken ct)
    {
        FeatureCatalog.FeatureDefinition? feature = FeatureCatalog.All.FirstOrDefault(f => f.Key == featureKey);
        List<FeaturePricing> rows = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == featureKey &&
                p.ChargeUnit == chargeUnit)
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        FeaturePricing? initial = rows.FirstOrDefault(p => p.BillingPeriod == PricingBillingPeriod.OneTime);
        FeaturePricing? renewal = rows.FirstOrDefault(p => p.BillingPeriod != PricingBillingPeriod.OneTime);
        RecurringPricingPolicy policy = RecurringPricingPolicyCodec.ReadFromNotes(initial?.Notes, renewal?.Notes);

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
        IQueryable<FeaturePricing> query = _db.FeaturePricings
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
