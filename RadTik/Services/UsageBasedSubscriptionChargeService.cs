using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Helpers;
using RadTik.Models;
using RadTik.Security;
using RadTik.Services.SystemAdminPricing;

namespace RadTik.Services;

/// <summary>
/// خصم فوري عند زيادة الاستخدام الفعلي لوحدات التسعير (لكل مشترك/مرسل/...).
/// يستخدم Ledger دائم في قاعدة البيانات لضمان الدقة بعد إعادة التشغيل.
/// </summary>
public sealed class UsageBasedSubscriptionChargeService : IUsageBasedSubscriptionChargeService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<UsageBasedSubscriptionChargeService> _logger;

    public UsageBasedSubscriptionChargeService(
        ApplicationDbContext db,
        ILogger<UsageBasedSubscriptionChargeService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task ChargeUsageIncreaseAsync(
        int companyNetworkId,
        string actorUserId,
        PricingChargeUnit? onlyUnit = null,
        CancellationToken ct = default)
    {
        await EnsureLedgerTableAsync(ct);

        var now = DateTime.Now;

        var subs = await _db.NetworkServiceSubscriptions
            .Where(s => s.NetworkId == companyNetworkId && s.Status == NetworkServiceSubscriptionStatus.Active)
            .ToListAsync(ct);

        if (subs.Count == 0)
        {
            // Trial fallback: when feature subscriptions are not yet provisioned,
            // allow charging per explicit unit (e.g. PerUser on employee creation)
            // using active pricing configured by SystemAdmin.
            if (onlyUnit.HasValue)
            {
                await TryChargeWithoutSubscriptionAsync(companyNetworkId, actorUserId, onlyUnit.Value, ct);
            }
            return;
        }

        var company = await _db.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct)
            ?? await _db.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (company == null)
        {
            return;
        }

        var networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);

        var matchedExplicitUnitPricing = false;
        foreach (var sub in subs)
        {
            var pricing = await _db.FeaturePricings
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.FeatureKey == sub.FeatureKey &&
                    p.BillingPeriod == sub.BillingPeriod, ct);

            if (pricing == null)
            {
                continue;
            }

            if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
            {
                continue;
            }

            if (onlyUnit.HasValue && pricing.ChargeUnit != onlyUnit.Value)
            {
                continue;
            }

            if (onlyUnit.HasValue && pricing.ChargeUnit == onlyUnit.Value)
            {
                matchedExplicitUnitPricing = true;
            }

            if (pricing.ChargeUnit is PricingChargeUnit.Flat or PricingChargeUnit.PerRequest or PricingChargeUnit.PerReport)
            {
                continue;
            }

            var currentUnitKeys = await GetCurrentUnitKeysAsync(networkIds, pricing.ChargeUnit, ct);
            var currentSet = currentUnitKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            var ledger = await _db.ServiceUnitChargeLedgers
                .Where(l => l.NetworkServiceSubscriptionId == sub.Id && l.ChargeUnit == pricing.ChargeUnit)
                .ToListAsync(ct);
            var existingByKey = ledger.ToDictionary(l => l.UnitEntityKey, StringComparer.OrdinalIgnoreCase);
            var initialPricing = await ResolveInitialPerUnitPricingAsync(
                sub.FeatureKey,
                pricing.ChargeUnit,
                fallbackBillingPeriod: sub.BillingPeriod,
                ct);
            var policy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, pricing);
            var provisionedUnitsCount = ledger.Count;

            foreach (var item in ledger)
            {
                var shouldBeActive = currentSet.Contains(item.UnitEntityKey);
                if (item.IsActive != shouldBeActive)
                {
                    item.IsActive = shouldBeActive;
                    item.UpdatedAt = now;
                }
            }

            foreach (var unitKey in currentSet)
            {
                if (existingByKey.TryGetValue(unitKey, out var existing))
                {
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        existing.UpdatedAt = now;
                    }
                    continue;
                }

                var chargeAmount = initialPricing != null
                    ? WalletMath.CeilSyp(initialPricing.AmountSYP)
                    : 0m;
                var shouldChargeThisUnit = provisionedUnitsCount >= policy.FreeInitialUnits;
                if (!shouldChargeThisUnit)
                {
                    chargeAmount = 0m;
                }
                if (chargeAmount > 0)
                {
                    if (company.Balance < chargeAmount)
                    {
                        sub.Status = NetworkServiceSubscriptionStatus.Suspended;
                        sub.UpdatedAt = now;
                        _logger.LogWarning(
                            "Suspended subscription #{SubscriptionId} due to insufficient balance for unit {UnitKey}. Required={Required}, Balance={Balance}",
                            sub.Id, unitKey, chargeAmount, company.Balance);
                        break;
                    }

                    var previousBalance = company.Balance;
                    company.Balance -= chargeAmount;

                    _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
                    {
                        NetworkId = companyNetworkId,
                        Type = NetworkWalletTransactionType.ServiceCharge,
                        SignedAmount = -chargeAmount,
                        PreviousBalance = previousBalance,
                        NewBalance = company.Balance,
                        NetworkServiceSubscriptionId = sub.Id,
                        CreatedByUserId = actorUserId,
                        CreatedAt = now,
                        Notes = $"خصم عنصر جديد: {sub.FeatureKey} / {(initialPricing?.BillingPeriod ?? sub.BillingPeriod)} / {pricing.ChargeUnit} / {unitKey}"
                    });
                }

                _db.ServiceUnitChargeLedgers.Add(new ServiceUnitChargeLedger
                {
                    NetworkServiceSubscriptionId = sub.Id,
                    ChargeUnit = pricing.ChargeUnit,
                    UnitEntityKey = unitKey,
                    IsActive = true,
                    FirstChargedAt = chargeAmount > 0 ? now : null,
                    LastChargedAt = chargeAmount > 0 ? now : null,
                    CreatedAt = now,
                    UpdatedAt = now
                });
                provisionedUnitsCount++;
            }
        }

        await _db.SaveChangesAsync(ct);

        if (onlyUnit.HasValue && !matchedExplicitUnitPricing)
        {
            await TryChargeWithoutSubscriptionAsync(companyNetworkId, actorUserId, onlyUnit.Value, ct);
        }
    }

    public async Task InitializeBaselineAsync(
        int companyNetworkId,
        int subscriptionId,
        CancellationToken ct = default)
    {
        await EnsureLedgerTableAsync(ct);

        var sub = await _db.NetworkServiceSubscriptions.FirstOrDefaultAsync(s =>
            s.Id == subscriptionId &&
            s.NetworkId == companyNetworkId, ct);
        if (sub == null)
        {
            return;
        }

        var pricing = await _db.FeaturePricings
            .AsNoTracking()
            .FirstOrDefaultAsync(p =>
                p.IsActive &&
                p.FeatureKey == sub.FeatureKey &&
                p.BillingPeriod == sub.BillingPeriod, ct);
        if (pricing == null)
        {
            return;
        }

        if (pricing.ChargeUnit == PricingChargeUnit.PercentOfCollectedAmount)
        {
            return;
        }

        if (pricing.ChargeUnit is PricingChargeUnit.Flat or PricingChargeUnit.PerRequest or PricingChargeUnit.PerReport)
        {
            return;
        }

        var networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);
        var currentUnitKeys = await GetCurrentUnitKeysAsync(networkIds, pricing.ChargeUnit, ct);
        var now = DateTime.Now;

        var existing = await _db.ServiceUnitChargeLedgers
            .Where(l => l.NetworkServiceSubscriptionId == subscriptionId && l.ChargeUnit == pricing.ChargeUnit)
            .Select(l => l.UnitEntityKey)
            .ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = currentUnitKeys
            .Where(k => !existingSet.Contains(k))
            .ToList();

        if (toAdd.Count == 0)
        {
            return;
        }

        _db.ServiceUnitChargeLedgers.AddRange(toAdd.Select(k => new ServiceUnitChargeLedger
        {
            NetworkServiceSubscriptionId = subscriptionId,
            ChargeUnit = pricing.ChargeUnit,
            UnitEntityKey = k,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        }));

        await _db.SaveChangesAsync(ct);
    }

    public async Task<string> ResolveActorUserIdAsync(int companyNetworkId, CancellationToken ct = default)
    {
        var company = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (company != null && !string.IsNullOrWhiteSpace(company.ManagerUserId))
        {
            return company.ManagerUserId!;
        }

        var sysAdminId = await (from u in _db.Users
            join ur in _db.UserRoles on u.Id equals ur.UserId
            join r in _db.Roles on ur.RoleId equals r.Id
            where r.Name == RoleNames.SystemAdministrator
            select u.Id).FirstOrDefaultAsync(ct);

        if (!string.IsNullOrWhiteSpace(sysAdminId))
        {
            return sysAdminId!;
        }

        return await _db.Users.Select(u => u.Id).FirstAsync(ct);
    }

    public async Task<UsageImportChargeEstimate> EstimateImportChargeAsync(
        int companyNetworkId,
        PricingChargeUnit chargeUnit,
        int importableCount,
        CancellationToken ct = default)
    {
        if (importableCount <= 0)
        {
            return new UsageImportChargeEstimate
            {
                ImportableCount = 0
            };
        }

        var company = await _db.Networks
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct)
            ?? await _db.Networks
                .AsNoTracking()
                .FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);

        if (company == null)
        {
            return new UsageImportChargeEstimate
            {
                ImportableCount = importableCount
            };
        }

        var subs = await _db.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s => s.NetworkId == companyNetworkId && s.Status == NetworkServiceSubscriptionStatus.Active)
            .ToListAsync(ct);

        if (subs.Count == 0)
        {
            var fallbackUnitPrice = await ResolveTrialUnitPriceAsync(chargeUnit, ct);
            return new UsageImportChargeEstimate
            {
                ImportableCount = importableCount,
                UnitPriceSyp = fallbackUnitPrice,
                RequiredAmountSyp = WalletMath.CeilSyp(fallbackUnitPrice * importableCount),
                MatchedPricingsCount = fallbackUnitPrice > 0 ? 1 : 0,
                WalletBalance = company.Balance
            };
        }

        var unitPrice = 0m;
        var required = 0m;
        var matchedPricings = 0;
        foreach (var sub in subs)
        {
            var pricing = await _db.FeaturePricings
                .AsNoTracking()
                .FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    p.FeatureKey == sub.FeatureKey &&
                    p.BillingPeriod == sub.BillingPeriod &&
                    p.ChargeUnit == chargeUnit, ct);

            if (pricing == null)
            {
                continue;
            }

            var initialPricing = await ResolveInitialPerUnitPricingAsync(
                sub.FeatureKey,
                chargeUnit,
                fallbackBillingPeriod: sub.BillingPeriod,
                ct);
            if (initialPricing == null)
            {
                continue;
            }

            var policy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, pricing);
            var existingUnitsCount = await _db.ServiceUnitChargeLedgers
                .AsNoTracking()
                .Where(l => l.NetworkServiceSubscriptionId == sub.Id && l.ChargeUnit == chargeUnit)
                .CountAsync(ct);
            var freeUnitsRemaining = Math.Max(0, policy.FreeInitialUnits - existingUnitsCount);
            var chargeableUnits = Math.Max(0, importableCount - freeUnitsRemaining);
            var effectiveUnitPrice = WalletMath.CeilSyp(initialPricing.AmountSYP);
            unitPrice += effectiveUnitPrice;
            required += WalletMath.CeilSyp(effectiveUnitPrice * chargeableUnits);
            matchedPricings++;
        }
        if (matchedPricings == 0)
        {
            var fallbackUnitPrice = await ResolveTrialUnitPriceAsync(chargeUnit, ct);
            return new UsageImportChargeEstimate
            {
                ImportableCount = importableCount,
                MatchedPricingsCount = fallbackUnitPrice > 0 ? 1 : 0,
                UnitPriceSyp = fallbackUnitPrice,
                RequiredAmountSyp = WalletMath.CeilSyp(fallbackUnitPrice * importableCount),
                WalletBalance = company.Balance
            };
        }

        return new UsageImportChargeEstimate
        {
            ImportableCount = importableCount,
            MatchedPricingsCount = matchedPricings,
            UnitPriceSyp = unitPrice,
            RequiredAmountSyp = required,
            WalletBalance = company.Balance
        };
    }

    public async Task<ReportExportChargeResult> TryChargeReportExportAsync(
        int companyNetworkId,
        string actorUserId,
        string reportDescription,
        CancellationToken ct = default)
    {
        var now = DateTime.Now;

        // وصول مدير الشركة للتقارير يخضع لسياسة الصلاحيات فقط؛ التسعير الفعلي يحدده مدير النظام عبر ReportsExport.
        var pricing = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p => p.IsActive && p.FeatureKey == FeatureKeys.ReportsExport)
            .OrderByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);

        if (pricing == null)
        {
            return new ReportExportChargeResult
            {
                Success = false,
                ErrorMessage = "لم يُعرّف سعر توليد التقارير من قبل مدير النظام (خدمة ReportsExport)."
            };
        }

        var amount = WalletMath.CeilSyp(pricing.AmountSYP);
        if (amount <= 0)
        {
            return new ReportExportChargeResult { Success = true, ChargedAmountSyp = 0 };
        }

        var company = await _db.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
        if (company == null)
        {
            return new ReportExportChargeResult { Success = false, ErrorMessage = "تعذر تحديد حساب الشركة." };
        }

        if (company.Balance < amount)
        {
            return new ReportExportChargeResult
            {
                Success = false,
                ErrorMessage = $"الرصيد غير كافٍ لتوليد التقرير. المطلوب: {amount:N2} ل.س.ج والرصيد الحالي: {company.Balance:N2} ل.س.ج."
            };
        }

        var previousBalance = company.Balance;
        company.Balance -= amount;

        _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
        {
            NetworkId = companyNetworkId,
            Type = NetworkWalletTransactionType.ServiceCharge,
            SignedAmount = -amount,
            PreviousBalance = previousBalance,
            NewBalance = company.Balance,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            Notes = $"توليد تقرير: {reportDescription} (تسعير #{pricing.Id}, {pricing.BillingPeriod})"
        });

        await _db.SaveChangesAsync(ct);

        return new ReportExportChargeResult { Success = true, ChargedAmountSyp = amount };
    }

    private async Task<List<string>> GetCurrentUnitKeysAsync(IReadOnlyList<int> networkIds, PricingChargeUnit unit, CancellationToken ct)
    {
        return unit switch
        {
            PricingChargeUnit.PerUser => await _db.Users
                .AsNoTracking()
                .Where(u => u.IsActive && u.NetworkId.HasValue && networkIds.Contains(u.NetworkId.Value))
                .Select(u => "U:" + u.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerNetwork => await _db.Networks
                .AsNoTracking()
                .Where(n => networkIds.Contains(n.Id))
                .Select(n => "N:" + n.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerSubscriber => await _db.Clients
                .AsNoTracking()
                .Where(c => c.IsActive && c.NetworkId.HasValue && networkIds.Contains(c.NetworkId.Value))
                .Select(c => "C:" + c.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerSector => await _db.Sectors
                .AsNoTracking()
                .Where(s => s.IsActive && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
                .Select(s => "S:" + s.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerReceiver => await _db.Receivers
                .AsNoTracking()
                .Where(r => r.IsActive && r.NetworkId.HasValue && networkIds.Contains(r.NetworkId.Value))
                .Select(r => "R:" + r.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerServer => await _db.MikroTikServers
                .AsNoTracking()
                .Where(s => s.IsActive && s.NetworkId.HasValue && networkIds.Contains(s.NetworkId.Value))
                .Select(s => "M:" + s.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerCollectionPoint => await _db.CollectionPointAccounts
                .AsNoTracking()
                .Where(a => a.NetworkId.HasValue && networkIds.Contains(a.NetworkId.Value))
                .Select(a => "P:" + a.Id)
                .ToListAsync(ct),

            PricingChargeUnit.PerSpeedProfile => await _db.Profiles
                .AsNoTracking()
                .Where(p => p.IsActive && p.NetworkId.HasValue && networkIds.Contains(p.NetworkId.Value))
                .Select(p => "F:" + p.Id)
                .ToListAsync(ct),

            _ => []
        };
    }

    private async Task EnsureLedgerTableAsync(CancellationToken ct)
    {
        const string sql = """
            IF OBJECT_ID(N'[dbo].[ServiceUnitChargeLedgers]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[ServiceUnitChargeLedgers](
                    [Id] BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [NetworkServiceSubscriptionId] INT NOT NULL,
                    [ChargeUnit] INT NOT NULL,
                    [UnitEntityKey] NVARCHAR(128) NOT NULL,
                    [IsActive] BIT NOT NULL CONSTRAINT [DF_ServiceUnitChargeLedgers_IsActive] DEFAULT(1),
                    [FirstChargedAt] DATETIME2 NULL,
                    [LastChargedAt] DATETIME2 NULL,
                    [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ServiceUnitChargeLedgers_CreatedAt] DEFAULT(GETDATE()),
                    [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_ServiceUnitChargeLedgers_UpdatedAt] DEFAULT(GETDATE())
                );

                CREATE UNIQUE INDEX [IX_ServiceUnitChargeLedgers_Sub_Unit]
                    ON [dbo].[ServiceUnitChargeLedgers]([NetworkServiceSubscriptionId],[ChargeUnit],[UnitEntityKey]);
            END
            """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task<FeaturePricing?> ResolveInitialPerUnitPricingAsync(
        string featureKey,
        PricingChargeUnit chargeUnit,
        PricingBillingPeriod fallbackBillingPeriod,
        CancellationToken ct)
    {
        // القاعدة العامة لكل الخدمات: قيمة الإضافة تُؤخذ من خيار OneTime إن وُجد،
        // وإلا نعود لسعر دورة الاشتراك الحالية كبديل.
        return await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == featureKey &&
                p.ChargeUnit == chargeUnit &&
                (p.BillingPeriod == PricingBillingPeriod.OneTime || p.BillingPeriod == fallbackBillingPeriod))
            .OrderBy(p => p.BillingPeriod == PricingBillingPeriod.OneTime ? 0 : 1)
            .ThenBy(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }

    private async Task TryChargeWithoutSubscriptionAsync(
        int companyNetworkId,
        string actorUserId,
        PricingChargeUnit chargeUnit,
        CancellationToken ct)
    {
        var featureKey = MapChargeUnitToFeatureKey(chargeUnit);
        if (featureKey is null)
        {
            return;
        }

        var pricing = await ResolveTrialPerUnitPricingAsync(featureKey, chargeUnit, ct);
        if (pricing == null)
        {
            return;
        }

        var chargeAmount = WalletMath.CeilSyp(pricing.AmountSYP);
        if (chargeAmount <= 0)
        {
            return;
        }

        var company = await _db.Networks
            .FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct)
            ?? await _db.Networks
                .FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (company == null)
        {
            return;
        }

        if (company.Balance < chargeAmount)
        {
            _logger.LogWarning(
                "Skipped trial fallback charge due to insufficient balance. Company={CompanyId}, Unit={Unit}, Required={Required}, Balance={Balance}",
                companyNetworkId, chargeUnit, chargeAmount, company.Balance);
            return;
        }

        var now = DateTime.Now;
        var previousBalance = company.Balance;
        company.Balance -= chargeAmount;

        _db.NetworkWalletTransactions.Add(new NetworkWalletTransaction
        {
            NetworkId = companyNetworkId,
            Type = NetworkWalletTransactionType.ServiceCharge,
            SignedAmount = -chargeAmount,
            PreviousBalance = previousBalance,
            NewBalance = company.Balance,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            Notes = $"خصم تجريبي (بدون اشتراك) للوحدة: {chargeUnit} / الخدمة: {featureKey}"
        });

        await _db.SaveChangesAsync(ct);
    }

    private async Task<decimal> ResolveTrialUnitPriceAsync(
        PricingChargeUnit chargeUnit,
        CancellationToken ct)
    {
        var featureKey = MapChargeUnitToFeatureKey(chargeUnit);
        if (featureKey is null)
        {
            return 0m;
        }

        var pricing = await ResolveTrialPerUnitPricingAsync(featureKey, chargeUnit, ct);

        return pricing == null
            ? 0m
            : WalletMath.CeilSyp(pricing.AmountSYP);
    }

    private static string? MapChargeUnitToFeatureKey(PricingChargeUnit chargeUnit)
    {
        return chargeUnit switch
        {
            PricingChargeUnit.PerUser => FeatureKeys.Users,
            PricingChargeUnit.PerSubscriber => FeatureKeys.Clients,
            PricingChargeUnit.PerSector => FeatureKeys.Sectors,
            PricingChargeUnit.PerReceiver => FeatureKeys.Receivers,
            PricingChargeUnit.PerServer => FeatureKeys.MikroTikServers,
            PricingChargeUnit.PerCollectionPoint => FeatureKeys.CollectionPoints,
            PricingChargeUnit.PerSpeedProfile => FeatureKeys.Profiles,
            PricingChargeUnit.PerNetwork => FeatureKeys.Networks,
            _ => null
        };
    }

    private async Task<FeaturePricing?> ResolveTrialPerUnitPricingAsync(
        string featureKey,
        PricingChargeUnit chargeUnit,
        CancellationToken ct)
    {
        // Trial mode: pick any active pricing for the unit/feature.
        // Prefer OneTime, then Monthly, then any other billing period.
        return await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.FeatureKey == featureKey &&
                p.ChargeUnit == chargeUnit)
            .OrderBy(p =>
                p.BillingPeriod == PricingBillingPeriod.OneTime ? 0 :
                p.BillingPeriod == PricingBillingPeriod.Monthly ? 1 : 2)
            .ThenByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .FirstOrDefaultAsync(ct);
    }
}

public sealed class UsageImportChargeEstimate
{
    public int ImportableCount { get; set; }
    public int MatchedPricingsCount { get; set; }
    public decimal UnitPriceSyp { get; set; }
    public decimal RequiredAmountSyp { get; set; }
    public decimal WalletBalance { get; set; }
    public bool HasCharge => UnitPriceSyp > 0m && RequiredAmountSyp > 0m;
    public bool HasSufficientBalance => WalletBalance >= RequiredAmountSyp;
}
