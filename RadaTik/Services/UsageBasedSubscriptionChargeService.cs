using Microsoft.EntityFrameworkCore;
using RadaTik.Constants;
using RadaTik.Data;
using RadaTik.Helpers;
using RadaTik.Models;
using RadaTik.Security;
using RadaTik.Services.SystemAdminPricing;

namespace RadaTik.Services;

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

        DateTime now = DateTime.Now;

        List<NetworkServiceSubscription> subs = await _db.NetworkServiceSubscriptions
            .Where(s => s.NetworkId == companyNetworkId && s.Status == NetworkServiceSubscriptionStatus.Active)
            .ToListAsync(ct);

        if (subs.Count == 0)
        {
            return;
        }

        Network? company = await _db.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct)
            ?? await _db.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (company == null)
        {
            return;
        }

        List<int> networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);

        foreach (NetworkServiceSubscription? sub in subs)
        {
            FeaturePricing? pricing = await _db.FeaturePricings
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

            if (pricing.ChargeUnit is PricingChargeUnit.Flat or PricingChargeUnit.PerRequest or PricingChargeUnit.PerReport)
            {
                continue;
            }

            List<string> currentUnitKeys = await GetCurrentUnitKeysAsync(networkIds, pricing.ChargeUnit, ct);
            HashSet<string> currentSet = currentUnitKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);

            List<ServiceUnitChargeLedger> ledger = await _db.ServiceUnitChargeLedgers
                .Where(l => l.NetworkServiceSubscriptionId == sub.Id && l.ChargeUnit == pricing.ChargeUnit)
                .ToListAsync(ct);
            Dictionary<string, ServiceUnitChargeLedger> existingByKey = ledger.ToDictionary(l => l.UnitEntityKey, StringComparer.OrdinalIgnoreCase);
            FeaturePricing? initialPricing = await ResolveInitialPerUnitPricingAsync(
                sub.FeatureKey,
                pricing.ChargeUnit,
                fallbackBillingPeriod: sub.BillingPeriod,
                ct);
            RecurringPricingPolicy policy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, pricing);
            int provisionedUnitsCount = ledger.Count;

            foreach (ServiceUnitChargeLedger? item in ledger)
            {
                bool shouldBeActive = currentSet.Contains(item.UnitEntityKey);
                if (item.IsActive != shouldBeActive)
                {
                    item.IsActive = shouldBeActive;
                    item.UpdatedAt = now;
                }
            }

            foreach (string? unitKey in currentSet)
            {
                if (existingByKey.TryGetValue(unitKey, out ServiceUnitChargeLedger? existing))
                {
                    if (!existing.IsActive)
                    {
                        existing.IsActive = true;
                        existing.UpdatedAt = now;
                    }
                    continue;
                }

                decimal chargeAmount = initialPricing != null
                    ? WalletMath.CeilSyp(initialPricing.AmountSYP)
                    : 0m;
                bool shouldChargeThisUnit = provisionedUnitsCount >= policy.FreeInitialUnits;
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

                    decimal previousBalance = company.Balance;
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
    }

    public async Task InitializeBaselineAsync(
        int companyNetworkId,
        int subscriptionId,
        CancellationToken ct = default)
    {
        await EnsureLedgerTableAsync(ct);

        NetworkServiceSubscription? sub = await _db.NetworkServiceSubscriptions.FirstOrDefaultAsync(s =>
            s.Id == subscriptionId &&
            s.NetworkId == companyNetworkId, ct);
        if (sub == null)
        {
            return;
        }

        FeaturePricing? pricing = await _db.FeaturePricings
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

        List<int> networkIds = await PricingChargeHelper.GetCompanyScopeNetworkIdsAsync(_db, companyNetworkId);
        List<string> currentUnitKeys = await GetCurrentUnitKeysAsync(networkIds, pricing.ChargeUnit, ct);
        DateTime now = DateTime.Now;

        List<string> existing = await _db.ServiceUnitChargeLedgers
            .Where(l => l.NetworkServiceSubscriptionId == subscriptionId && l.ChargeUnit == pricing.ChargeUnit)
            .Select(l => l.UnitEntityKey)
            .ToListAsync(ct);
        HashSet<string> existingSet = existing.ToHashSet(StringComparer.OrdinalIgnoreCase);

        List<string> toAdd = currentUnitKeys
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
        Network? company = await _db.Networks.AsNoTracking().FirstOrDefaultAsync(n => n.Id == companyNetworkId, ct);
        if (company != null && !string.IsNullOrWhiteSpace(company.ManagerUserId))
        {
            return company.ManagerUserId!;
        }

        string? sysAdminId = await (from u in _db.Users
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

        Network? company = await _db.Networks
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

        List<NetworkServiceSubscription> subs = await _db.NetworkServiceSubscriptions
            .AsNoTracking()
            .Where(s => s.NetworkId == companyNetworkId && s.Status == NetworkServiceSubscriptionStatus.Active)
            .ToListAsync(ct);

        if (subs.Count == 0)
        {
            return new UsageImportChargeEstimate
            {
                ImportableCount = importableCount,
                WalletBalance = company.Balance
            };
        }

        decimal unitPrice = 0m;
        decimal required = 0m;
        int matchedPricings = 0;
        string[] featureKeys = subs
            .Select(s => s.FeatureKey)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        PricingBillingPeriod[] billingPeriods = subs
            .Select(s => s.BillingPeriod)
            .Distinct()
            .ToArray();

        List<FeaturePricing> activePricings = await _db.FeaturePricings
            .AsNoTracking()
            .Where(p =>
                p.IsActive &&
                p.ChargeUnit == chargeUnit &&
                featureKeys.Contains(p.FeatureKey) &&
                (billingPeriods.Contains(p.BillingPeriod) || p.BillingPeriod == PricingBillingPeriod.OneTime))
            .OrderByDescending(p => p.UpdatedAt)
            .ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        Dictionary<(string FeatureKey, PricingBillingPeriod BillingPeriod), FeaturePricing> recurringPricingByKey = activePricings
            .Where(p => p.BillingPeriod != PricingBillingPeriod.OneTime)
            .GroupBy(p => (p.FeatureKey, p.BillingPeriod))
            .ToDictionary(g => g.Key, g => g.First());
        Dictionary<string, FeaturePricing> initialPricingByFeature = activePricings
            .Where(p => p.BillingPeriod == PricingBillingPeriod.OneTime)
            .GroupBy(p => p.FeatureKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        int[] subscriptionIds = subs.Select(s => s.Id).ToArray();
        Dictionary<int, int> existingUnitsCountBySubscription = await _db.ServiceUnitChargeLedgers
            .AsNoTracking()
            .Where(l => l.ChargeUnit == chargeUnit && subscriptionIds.Contains(l.NetworkServiceSubscriptionId))
            .GroupBy(l => l.NetworkServiceSubscriptionId)
            .Select(g => new { SubscriptionId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.SubscriptionId, x => x.Count, ct);

        foreach (NetworkServiceSubscription? sub in subs)
        {
            if (!recurringPricingByKey.TryGetValue((sub.FeatureKey, sub.BillingPeriod), out FeaturePricing? pricing))
            {
                continue;
            }

            if (!initialPricingByFeature.TryGetValue(sub.FeatureKey, out FeaturePricing? initialPricing))
            {
                continue;
            }

            RecurringPricingPolicy policy = RecurringPricingPolicyCodec.ReadFromPricings(initialPricing, pricing);
            int existingUnitsCount = existingUnitsCountBySubscription.TryGetValue(sub.Id, out int count) ? count : 0;
            int freeUnitsRemaining = Math.Max(0, policy.FreeInitialUnits - existingUnitsCount);
            int chargeableUnits = Math.Max(0, importableCount - freeUnitsRemaining);
            decimal effectiveUnitPrice = WalletMath.CeilSyp(initialPricing.AmountSYP);
            unitPrice += effectiveUnitPrice;
            required += WalletMath.CeilSyp(effectiveUnitPrice * chargeableUnits);
            matchedPricings++;
        }
        if (matchedPricings == 0)
        {
            return new UsageImportChargeEstimate
            {
                ImportableCount = importableCount,
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
        DateTime now = DateTime.Now;

        // وصول مدير الشركة للتقارير يخضع لسياسة الصلاحيات فقط؛ التسعير الفعلي يحدده مدير النظام عبر ReportsExport.
        FeaturePricing? pricing = await _db.FeaturePricings
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

        decimal amount = WalletMath.CeilSyp(pricing.AmountSYP);
        if (amount <= 0)
        {
            return new ReportExportChargeResult { Success = true, ChargedAmountSyp = 0 };
        }

        Network? company = await _db.Networks.FirstOrDefaultAsync(n => n.Id == companyNetworkId && n.ParentNetworkId == null, ct);
        if (company == null)
        {
            return new ReportExportChargeResult { Success = false, ErrorMessage = "تعذر تحديد حساب الشركة." };
        }

        if (company.Balance < amount)
        {
            return new ReportExportChargeResult
            {
                Success = false,
                ErrorMessage = AppMessages.InsufficientBalance
            };
        }

        decimal previousBalance = company.Balance;
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

            // الشبكة الرئيسية تُفوتر مرة واحدة عبر MainNetworkCreationBilling فقط.
            // نفس قاعدة PricingChargeHelper: أول شبكة لا تُحسب كوحدة استخدام مدفوعة.
            PricingChargeUnit.PerNetwork => await _db.Networks
                .AsNoTracking()
                .Where(n => networkIds.Contains(n.Id) && n.ParentNetworkId != null)
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
        // Schema bootstrap at runtime was removed to keep schema evolution migration-driven.
        await Task.CompletedTask;
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
