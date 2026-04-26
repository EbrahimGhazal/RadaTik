using Microsoft.EntityFrameworkCore;
using RadTik.Data;
using RadTik.Models;
using RadTik.Security;

namespace RadTik.Services.SystemAdminPricing;

public static class StandalonePricingHandlerKeys
{
    public const string Reports = "Reports";
    public const string ProfileTax = "ProfileTax";
    public const string MaintenanceCommission = "MaintenanceCommission";
}

public interface IStandaloneServicePricingHandler
{
    string HandlerKey { get; }
}

public sealed class StandalonePricingUpdateResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
}

public interface IReportPricingHandler : IStandaloneServicePricingHandler
{
    Task<StandalonePricingUpdateResult> UpdateAsync(decimal initialPriceSyp, CancellationToken ct = default);
}

public interface IProfileTaxPricingHandler : IStandaloneServicePricingHandler
{
    Task<StandalonePricingUpdateResult> UpdateAsync(decimal taxPercentage, CancellationToken ct = default);
}

public interface IMaintenanceCommissionPricingHandler : IStandaloneServicePricingHandler
{
    Task<StandalonePricingUpdateResult> UpdateAsync(
        MaintenanceCommissionMode commissionMode,
        decimal commissionValue,
        CancellationToken ct = default);
}

public interface IStandaloneServicePricingHandlerResolver
{
    bool TryResolveReport(out IReportPricingHandler? handler);
    bool TryResolveProfileTax(out IProfileTaxPricingHandler? handler);
    bool TryResolveMaintenanceCommission(out IMaintenanceCommissionPricingHandler? handler);
}

public sealed class StandaloneServicePricingHandlerResolver : IStandaloneServicePricingHandlerResolver
{
    private readonly IReportPricingHandler? _reportHandler;
    private readonly IProfileTaxPricingHandler? _profileTaxHandler;
    private readonly IMaintenanceCommissionPricingHandler? _maintenanceCommissionHandler;

    public StandaloneServicePricingHandlerResolver(
        IReportPricingHandler? reportHandler,
        IProfileTaxPricingHandler? profileTaxHandler,
        IMaintenanceCommissionPricingHandler? maintenanceCommissionHandler)
    {
        _reportHandler = reportHandler;
        _profileTaxHandler = profileTaxHandler;
        _maintenanceCommissionHandler = maintenanceCommissionHandler;
    }

    public bool TryResolveReport(out IReportPricingHandler? handler)
    {
        handler = _reportHandler;
        return handler != null;
    }

    public bool TryResolveProfileTax(out IProfileTaxPricingHandler? handler)
    {
        handler = _profileTaxHandler;
        return handler != null;
    }

    public bool TryResolveMaintenanceCommission(out IMaintenanceCommissionPricingHandler? handler)
    {
        handler = _maintenanceCommissionHandler;
        return handler != null;
    }
}

internal static class StandalonePricingRowMutator
{
    public static void MarkInactive(IEnumerable<FeaturePricing> rows, DateTime now)
    {
        foreach (var row in rows)
        {
            row.IsActive = false;
            row.UpdatedAt = now;
        }
    }
}

internal static class StandalonePricingRecordStore
{
    public static async Task<FeaturePricing> GetOrCreateOneTimeAsync(
        ApplicationDbContext db,
        string featureKey,
        PricingChargeUnit? chargeUnit,
        DateTime now,
        CancellationToken ct)
    {
        var query = db.FeaturePricings.Where(p =>
            p.FeatureKey == featureKey &&
            p.BillingPeriod == PricingBillingPeriod.OneTime);

        if (chargeUnit.HasValue)
        {
            query = query.Where(p => p.ChargeUnit == chargeUnit.Value);
        }

        var pricing = await query.FirstOrDefaultAsync(ct);
        if (pricing != null)
        {
            return pricing;
        }

        pricing = new FeaturePricing
        {
            FeatureKey = featureKey,
            BillingPeriod = PricingBillingPeriod.OneTime,
            ChargeUnit = chargeUnit ?? PricingChargeUnit.Flat,
            Currency = PricingCurrency.SYP_New,
            IsActive = true,
            CreatedAt = now
        };

        db.FeaturePricings.Add(pricing);
        return pricing;
    }
}

public sealed class ReportPricingHandler : IReportPricingHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ReportPricingHandler> _logger;

    public ReportPricingHandler(ApplicationDbContext db, ILogger<ReportPricingHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string HandlerKey => StandalonePricingHandlerKeys.Reports;

    public async Task<StandalonePricingUpdateResult> UpdateAsync(decimal initialPriceSyp, CancellationToken ct = default)
    {
        if (initialPriceSyp < 0m)
        {
            return new StandalonePricingUpdateResult
            {
                Success = false,
                Message = "سعر إنشاء التقرير يجب أن يكون أكبر من أو يساوي صفر."
            };
        }

        try
        {
            var now = DateTime.UtcNow;

            var reportPricing = await StandalonePricingRecordStore.GetOrCreateOneTimeAsync(
                _db,
                FeatureKeys.ReportsExport,
                PricingChargeUnit.PerReport,
                now,
                ct);

            reportPricing.AmountSYP = initialPriceSyp;
            reportPricing.AmountUSD = 0m;
            reportPricing.Currency = PricingCurrency.SYP_New;
            reportPricing.IsActive = true;
            reportPricing.Notes = "تسعير إنشاء/توليد تقرير بواسطة مدير الشركة (خصم فوري لكل تقرير).";
            reportPricing.UpdatedAt = now;

            var duplicateRows = await _db.FeaturePricings
                .Where(p =>
                    p.FeatureKey == FeatureKeys.ReportsExport &&
                    p.ChargeUnit == PricingChargeUnit.PerReport &&
                    p.BillingPeriod != PricingBillingPeriod.OneTime)
                .ToListAsync(ct);

            StandalonePricingRowMutator.MarkInactive(duplicateRows, now);

            await _db.SaveChangesAsync(ct);
            return new StandalonePricingUpdateResult { Success = true, Message = "تم حفظ سعر خدمة التقارير بنجاح." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update reports pricing.");
            return new StandalonePricingUpdateResult { Success = false, Message = "تعذر حفظ سعر خدمة التقارير." };
        }
    }
}

public sealed class ProfileTaxPricingHandler : IProfileTaxPricingHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<ProfileTaxPricingHandler> _logger;

    public ProfileTaxPricingHandler(ApplicationDbContext db, ILogger<ProfileTaxPricingHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string HandlerKey => StandalonePricingHandlerKeys.ProfileTax;

    public async Task<StandalonePricingUpdateResult> UpdateAsync(decimal taxPercentage, CancellationToken ct = default)
    {
        if (taxPercentage < 0m || taxPercentage > 100m)
        {
            return new StandalonePricingUpdateResult
            {
                Success = false,
                Message = "ضريبة سعر البروفايل يجب أن تكون بين 0% و 100%."
            };
        }

        try
        {
            var now = DateTime.UtcNow;

            var profileTaxPricing = await StandalonePricingRecordStore.GetOrCreateOneTimeAsync(
                _db,
                FeatureKeys.ProfilePriceTax,
                PricingChargeUnit.Flat,
                now,
                ct);

            profileTaxPricing.AmountSYP = taxPercentage;
            profileTaxPricing.AmountUSD = 0m;
            profileTaxPricing.Currency = PricingCurrency.SYP_New;
            profileTaxPricing.IsActive = true;
            profileTaxPricing.Notes = "خدمة مستقلة: نسبة ضريبة سعر البروفايل المطبقة على مدير الشركة.";
            profileTaxPricing.UpdatedAt = now;

            var duplicateRows = await _db.FeaturePricings
                .Where(p =>
                    p.FeatureKey == FeatureKeys.ProfilePriceTax &&
                    (p.ChargeUnit != PricingChargeUnit.Flat || p.BillingPeriod != PricingBillingPeriod.OneTime))
                .ToListAsync(ct);

            StandalonePricingRowMutator.MarkInactive(duplicateRows, now);

            await _db.SaveChangesAsync(ct);
            return new StandalonePricingUpdateResult { Success = true, Message = "تم حفظ نسبة ضريبة سعر البروفايل بنجاح." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update profile price tax.");
            return new StandalonePricingUpdateResult { Success = false, Message = "تعذر حفظ نسبة ضريبة سعر البروفايل." };
        }
    }
}

public sealed class MaintenanceCommissionPricingHandler : IMaintenanceCommissionPricingHandler
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<MaintenanceCommissionPricingHandler> _logger;

    public MaintenanceCommissionPricingHandler(ApplicationDbContext db, ILogger<MaintenanceCommissionPricingHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public string HandlerKey => StandalonePricingHandlerKeys.MaintenanceCommission;

    public async Task<StandalonePricingUpdateResult> UpdateAsync(
        MaintenanceCommissionMode commissionMode,
        decimal commissionValue,
        CancellationToken ct = default)
    {
        if (commissionValue < 0m)
        {
            return new StandalonePricingUpdateResult
            {
                Success = false,
                Message = "قيمة عمولة الصيانة يجب أن تكون أكبر من أو تساوي صفر."
            };
        }

        try
        {
            var now = DateTime.UtcNow;
            var chargeUnit = commissionMode == MaintenanceCommissionMode.Percent
                ? PricingChargeUnit.PercentOfCollectedAmount
                : PricingChargeUnit.Flat;

            var maintenanceCommission = await StandalonePricingRecordStore.GetOrCreateOneTimeAsync(
                _db,
                FeatureKeys.MaintenanceCommission,
                null,
                now,
                ct);

            maintenanceCommission.ChargeUnit = chargeUnit;
            maintenanceCommission.AmountSYP = commissionValue;
            maintenanceCommission.AmountUSD = 0m;
            maintenanceCommission.Currency = PricingCurrency.SYP_New;
            maintenanceCommission.IsActive = true;
            maintenanceCommission.Notes = commissionMode == MaintenanceCommissionMode.Percent
                ? "عمولة طلبات الصيانة كنسبة مئوية تُضاف إلى إجمالي فاتورة الصيانة."
                : "عمولة طلبات الصيانة كمبلغ ثابت يُضاف إلى إجمالي فاتورة الصيانة.";
            maintenanceCommission.UpdatedAt = now;

            var duplicateRows = await _db.FeaturePricings
                .Where(p =>
                    p.FeatureKey == FeatureKeys.MaintenanceCommission &&
                    p.BillingPeriod == PricingBillingPeriod.OneTime &&
                    p.Id != maintenanceCommission.Id)
                .ToListAsync(ct);

            StandalonePricingRowMutator.MarkInactive(duplicateRows, now);

            await _db.SaveChangesAsync(ct);
            return new StandalonePricingUpdateResult { Success = true, Message = "تم حفظ عمولة طلبات الصيانة بنجاح." };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update maintenance commission pricing.");
            return new StandalonePricingUpdateResult { Success = false, Message = "تعذر حفظ عمولة طلبات الصيانة." };
        }
    }
}
